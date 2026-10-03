using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Billing;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Billing;

public interface ICreateOrderReceiptUseCase
{
    Task<AppResult<BillingDocumentResponse>> ExecuteAsync(int orderId, string? idempotencyKey, CancellationToken cancellationToken = default);
}

public interface IGetOrderBillingDocumentsUseCase
{
    Task<AppResult<List<BillingDocumentResponse>>> ExecuteAsync(int orderId, CancellationToken cancellationToken = default);
}

public interface IGetBillingDocumentPdfUseCase
{
    Task<AppResult<BillingDocumentPdfResponse>> ExecuteAsync(int orderId, Guid documentId, CancellationToken cancellationToken = default);
}

public interface IGetArcaConnectionStatusUseCase
{
    Task<AppResult<ArcaConnectionStatus>> ExecuteAsync(CancellationToken cancellationToken = default);
}

public sealed class CreateOrderReceiptUseCase(
    IApplicationDbContext db,
    IBillingProfile billingProfile,
    TimeProvider timeProvider,
    IAuditContext? auditContext = null) : ICreateOrderReceiptUseCase
{
    public async Task<AppResult<BillingDocumentResponse>> ExecuteAsync(int orderId, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var key = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > ValidationLimits.IdempotencyKey)
            return AppResult<BillingDocumentResponse>.Failure(AppErrorType.Validation, "La clave de idempotencia del comprobante no es valida.");

        var documentForKey = await db.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);

        if (documentForKey is not null)
        {
            if (documentForKey.OrderId != orderId || documentForKey.Category != BillingDocumentCategory.Receipt)
                return AppResult<BillingDocumentResponse>.Failure(AppErrorType.Conflict, "La clave de idempotencia ya fue usada para otro comprobante.");
            return AppResult<BillingDocumentResponse>.Success(BillingDocumentMapper.ToResponse(documentForKey));
        }

        var existing = await db.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.OrderId == orderId && x.Category == BillingDocumentCategory.Receipt, cancellationToken);
        if (existing is not null)
            return AppResult<BillingDocumentResponse>.Success(BillingDocumentMapper.ToResponse(existing));

        if (billingProfile.Mode != BillingMode.ReceiptOnly)
            return AppResult<BillingDocumentResponse>.Failure(
                AppErrorType.Conflict,
                "La emision fiscal todavia no esta habilitada: falta integrar la autorizacion electronica de ARCA.",
                "electronic_invoicing_not_implemented");

        var order = await db.Orders
            .Include(x => x.User)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return AppResult<BillingDocumentResponse>.Failure(AppErrorType.NotFound, "Pedido no encontrado.");

        if (order.Status is not (OrderStatus.Paid or OrderStatus.Preparing or OrderStatus.Shipped or OrderStatus.Delivered))
            return AppResult<BillingDocumentResponse>.Failure(AppErrorType.Conflict, "Solo se puede generar un comprobante para un pedido pagado.");

        var approvedPayment = order.Payments
            .Where(x => x.Status == PaymentStatus.Approved)
            .OrderByDescending(x => x.PaidAt ?? x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();
        if (order.Total > 0 && approvedPayment is null)
            return AppResult<BillingDocumentResponse>.Failure(AppErrorType.Conflict, "El pedido no tiene un pago aprobado para respaldar el comprobante.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var document = new BillingDocument
        {
            OrderId = order.Id,
            PaymentId = approvedPayment?.Id,
            IdempotencyKey = key,
            Category = BillingDocumentCategory.Receipt,
            Type = BillingDocumentType.PurchaseReceipt,
            Status = BillingDocumentStatus.Authorized,
            Currency = approvedPayment?.Currency ?? "ARS",
            IssuerBusinessName = billingProfile.BusinessName.Trim(),
            IssuerCuit = billingProfile.Cuit.Trim(),
            IssuerTaxCondition = billingProfile.TaxCondition,
            IssuerFiscalAddress = billingProfile.FiscalAddress.Trim(),
            IssuerGrossIncomeNumber = billingProfile.GrossIncomeNumber.Trim(),
            IssuerActivityStartDate = billingProfile.ActivityStartDate,
            RecipientName = $"{order.User.Name} {order.User.LastName}".Trim(),
            RecipientDocumentType = FiscalIdentityDocumentType.None,
            RecipientTaxCondition = RecipientTaxCondition.ConsumerFinal,
            RecipientEmail = order.User.Email,
            RecipientAddress = order.ShippingAddress,
            Subtotal = order.Subtotal,
            DiscountAmount = order.DiscountAmount,
            ShippingAmount = order.ShippingCost,
            Total = order.Total,
            AuthorizationProvider = "Internal",
            AuthorizedAtUtc = now,
            CreatedAtUtc = now
        };

        foreach (var item in order.Items.OrderBy(x => x.Id))
        {
            document.Items.Add(new BillingDocumentItem
            {
                OrderItemId = item.Id,
                Description = string.IsNullOrWhiteSpace(item.VariantSku) ? item.ProductName : $"{item.ProductName} ({item.VariantSku})",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                NetAmount = item.Subtotal,
                TotalAmount = item.Subtotal
            });
        }

        db.BillingDocuments.Add(document);
        AuditTrail.Add(db, auditContext, "PurchaseReceiptCreated", "Order", order.Id, null,
            new { documentId = document.Id, status = document.Status.ToString(), type = document.Type.ToString(), total = document.Total },
            "Comprobante interno generado manualmente.");
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.BillingDocuments.Remove(document);
            var winner = await db.BillingDocuments
                .AsNoTracking()
                .Include(x => x.Items)
                .Where(x => x.IdempotencyKey == key ||
                            (x.OrderId == orderId && x.Category == BillingDocumentCategory.Receipt))
                .OrderByDescending(x => x.IdempotencyKey == key)
                .ThenByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (winner is null) throw;
            if (winner.IdempotencyKey == key && winner.OrderId != orderId)
                return AppResult<BillingDocumentResponse>.Failure(AppErrorType.Conflict, "La clave de idempotencia ya fue usada para otro comprobante.");
            return AppResult<BillingDocumentResponse>.Success(BillingDocumentMapper.ToResponse(winner));
        }

        return AppResult<BillingDocumentResponse>.Success(BillingDocumentMapper.ToResponse(document));
    }
}

public sealed class GetOrderBillingDocumentsUseCase(IApplicationDbContext db) : IGetOrderBillingDocumentsUseCase
{
    public async Task<AppResult<List<BillingDocumentResponse>>> ExecuteAsync(int orderId, CancellationToken cancellationToken = default)
    {
        if (!await db.Orders.AsNoTracking().AnyAsync(x => x.Id == orderId, cancellationToken))
            return AppResult<List<BillingDocumentResponse>>.Failure(AppErrorType.NotFound, "Pedido no encontrado.");

        var documents = await db.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return AppResult<List<BillingDocumentResponse>>.Success(documents.Select(BillingDocumentMapper.ToResponse).ToList());
    }
}

public sealed class GetBillingDocumentPdfUseCase(
    IApplicationDbContext db,
    IReceiptPdfRenderer renderer) : IGetBillingDocumentPdfUseCase
{
    public async Task<AppResult<BillingDocumentPdfResponse>> ExecuteAsync(
        int orderId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await db.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == documentId && x.OrderId == orderId, cancellationToken);

        if (document is null)
            return AppResult<BillingDocumentPdfResponse>.Failure(AppErrorType.NotFound, "Comprobante no encontrado.");
        if (document.Category != BillingDocumentCategory.Receipt || document.Type != BillingDocumentType.PurchaseReceipt)
            return AppResult<BillingDocumentPdfResponse>.Failure(AppErrorType.Conflict, "El documento solicitado no es un comprobante interno descargable.");
        if (document.Status != BillingDocumentStatus.Authorized)
            return AppResult<BillingDocumentPdfResponse>.Failure(AppErrorType.Conflict, "El comprobante todavia no esta autorizado.");

        var content = renderer.Render(document);
        return AppResult<BillingDocumentPdfResponse>.Success(new BillingDocumentPdfResponse(
            content,
            $"pedido-{orderId}-comprobante-{document.Id:N}.pdf"));
    }
}

public sealed class GetArcaConnectionStatusUseCase(IArcaElectronicInvoiceGateway gateway) : IGetArcaConnectionStatusUseCase
{
    public async Task<AppResult<ArcaConnectionStatus>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        AppResult<ArcaConnectionStatus>.Success(await gateway.CheckConnectionAsync(cancellationToken));
}

internal static class BillingDocumentMapper
{
    public static BillingDocumentResponse ToResponse(BillingDocument document) => new(
        document.Id,
        document.OrderId,
        document.PaymentId,
        document.Category.ToString(),
        document.Type.ToString(),
        document.Status.ToString(),
        document.Currency,
        document.IssuerBusinessName,
        document.RecipientName,
        document.RecipientEmail,
        document.RecipientAddress,
        document.Subtotal,
        document.DiscountAmount,
        document.ShippingAmount,
        document.Total,
        document.CreatedAtUtc,
        document.AuthorizedAtUtc,
        document.Items.OrderBy(x => x.Id).Select(x => new BillingDocumentItemResponse(
            x.Id, x.OrderItemId, x.Description, x.Quantity, x.UnitPrice, x.DiscountAmount, x.TotalAmount)).ToList());
}
