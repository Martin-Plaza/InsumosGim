using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Payments;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Payments;

public interface ICreatePaymentUseCase
{
    Task<AppResult<PaymentResponse>> ExecuteAsync(int orderId, int userId, bool canManageAll, CreatePaymentRequest request, CancellationToken cancellationToken = default);
}

public interface ICreateCurrentPaymentUseCase
{
    Task<AppResult<PaymentResponse>> ExecuteAsync(int userId, CreatePaymentRequest request, CancellationToken cancellationToken = default);
}

public interface IGetPaymentByIdUseCase
{
    Task<AppResult<PaymentResponse>> ExecuteAsync(int id, int userId, bool canManageAll, CancellationToken cancellationToken = default);
}

public interface IGetOrderPaymentsUseCase
{
    Task<AppResult<List<PaymentResponse>>> ExecuteAsync(int orderId, int userId, bool canManageAll, CancellationToken cancellationToken = default);
}

public interface IUpdatePaymentStatusUseCase
{
    Task<AppResult<PaymentResponse>> ExecuteAsync(int id, UpdatePaymentStatusRequest request, CancellationToken cancellationToken = default);
}

public interface IHandlePaymentWebhookUseCase
{
    Task<AppResult<PaymentResponse>> ExecuteAsync(string provider, string providerPaymentId, CancellationToken cancellationToken = default);
}

public class CreatePaymentUseCase : ICreatePaymentUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public CreatePaymentUseCase(IApplicationDbContext db, IEnumerable<IPaymentGateway> gateways)
    {
        _db = db;
        _gateways = gateways;
    }

    public async Task<AppResult<PaymentResponse>> ExecuteAsync(int orderId, int userId, bool canManageAll, CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders
            .Include(x => x.User)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.NotFound, "Pedido no encontrado.");
        }

        if (order.UserId != userId && !canManageAll)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Forbidden, "No tenes permisos para pagar este pedido.");
        }

        if (order.Status == OrderStatus.Paid)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "El pedido ya esta pagado.");
        }

        if (order.Status == OrderStatus.Canceled || order.Status == OrderStatus.Shipped)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "El pedido no admite nuevos pagos.");
        }

        return await PaymentCreator.CreateAsync(_db, _gateways, order, request, cancellationToken);
    }
}

public class CreateCurrentPaymentUseCase : ICreateCurrentPaymentUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public CreateCurrentPaymentUseCase(IApplicationDbContext db, IEnumerable<IPaymentGateway> gateways)
    {
        _db = db;
        _gateways = gateways;
    }

    public async Task<AppResult<PaymentResponse>> ExecuteAsync(int userId, CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var pendingOrders = await _db.Orders
            .Include(x => x.User)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Where(x => x.UserId == userId && x.Status == OrderStatus.Pending)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        if (pendingOrders.Count == 0)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.NotFound, "No tenes una orden pendiente para pagar.");
        }

        if (pendingOrders.Count > 1)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "Tenes mas de una orden pendiente. Resolve una antes de continuar.");
        }

        return await PaymentCreator.CreateAsync(_db, _gateways, pendingOrders[0], request, cancellationToken);
    }
}

internal static class PaymentCreator
{
    public static async Task<AppResult<PaymentResponse>> CreateAsync(
        IApplicationDbContext db,
        IEnumerable<IPaymentGateway> gateways,
        Order order,
        CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            var idempotentPayment = await db.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

            if (idempotentPayment is not null)
            {
                if (idempotentPayment.OrderId != order.Id)
                {
                    return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "La clave de idempotencia ya fue usada en otra orden.");
                }

                return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(idempotentPayment));
            }
        }

        var activePayment = order.Payments
            .Where(x => x.Status == PaymentStatus.Pending)
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();

        if (activePayment is not null)
        {
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(activePayment));
        }

        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "Mock" : request.Provider.Trim();
        var gateway = gateways.FirstOrDefault(x => x.CanHandle(provider));
        if (gateway is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, $"Proveedor de pago no soportado: {provider}.");
        }

        PaymentPreferenceResult preference;
        try
        {
            preference = await gateway.CreatePreferenceAsync(order, request.IdempotencyKey, cancellationToken);
        }
        catch (PaymentGatewayException ex)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, ex.Message);
        }

        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = preference.Provider,
            ExternalReference = $"order-{order.Id}",
            ProviderPreferenceId = preference.ProviderPreferenceId,
            IdempotencyKey = idempotencyKey,
            Amount = order.Total,
            Currency = "ARS",
            Status = PaymentStatus.Pending,
            CheckoutUrl = preference.CheckoutUrl
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);

        return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
    }
}
public class GetPaymentByIdUseCase : IGetPaymentByIdUseCase
{
    private readonly IApplicationDbContext _db;

    public GetPaymentByIdUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<PaymentResponse>> ExecuteAsync(int id, int userId, bool canManageAll, CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(x => x.Order)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (payment is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.NotFound, "Pago no encontrado.");
        }

        if (payment.Order.UserId != userId && !canManageAll)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Forbidden, "No tenes permisos para ver este pago.");
        }

        return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
    }
}

public class GetOrderPaymentsUseCase : IGetOrderPaymentsUseCase
{
    private readonly IApplicationDbContext _db;

    public GetOrderPaymentsUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<List<PaymentResponse>>> ExecuteAsync(int orderId, int userId, bool canManageAll, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return AppResult<List<PaymentResponse>>.Failure(AppErrorType.NotFound, "Pedido no encontrado.");
        }

        if (order.UserId != userId && !canManageAll)
        {
            return AppResult<List<PaymentResponse>>.Failure(AppErrorType.Forbidden, "No tenes permisos para ver los pagos de este pedido.");
        }

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.Id)
            .Select(x => PaymentMapper.ToResponse(x))
            .ToListAsync(cancellationToken);

        return AppResult<List<PaymentResponse>>.Success(payments);
    }
}

public class UpdatePaymentStatusUseCase : IUpdatePaymentStatusUseCase
{
    private readonly IApplicationDbContext _db;

    public UpdatePaymentStatusUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<PaymentResponse>> ExecuteAsync(int id, UpdatePaymentStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<PaymentStatus>(request.Status, true, out var newStatus))
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "Estado de pago invalido.");
        }

        var payment = await PaymentQueries.LoadTrackedPaymentAsync(_db, id, cancellationToken);
        if (payment is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.NotFound, "Pago no encontrado.");
        }

        return await PaymentStatusApplier.ApplyAsync(_db, payment, newStatus, request.ProviderPaymentId, request.FailureReason, cancellationToken);
    }
}

public class HandlePaymentWebhookUseCase : IHandlePaymentWebhookUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;

    public HandlePaymentWebhookUseCase(IApplicationDbContext db, IEnumerable<IPaymentGateway> gateways)
    {
        _db = db;
        _gateways = gateways;
    }

    public async Task<AppResult<PaymentResponse>> ExecuteAsync(string provider, string providerPaymentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerPaymentId))
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "El id del pago del proveedor es obligatorio.");
        }

        var gateway = _gateways.FirstOrDefault(x => x.CanHandle(provider));
        if (gateway is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, $"Proveedor de pago no soportado: {provider}.");
        }

        ProviderPaymentResult providerPayment;
        try
        {
            providerPayment = await gateway.GetPaymentAsync(providerPaymentId, cancellationToken);
        }
        catch (PaymentGatewayException ex)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, ex.Message);
        }

        var orderId = PaymentExternalReferences.TryGetOrderId(providerPayment.ExternalReference);
        if (orderId is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "La referencia externa del pago no corresponde a una orden valida.");
        }

        var payment = await _db.Payments
            .Include(x => x.Order)
            .ThenInclude(x => x.Items)
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x =>
                x.Provider == provider &&
                (x.ProviderPaymentId == providerPayment.ProviderPaymentId ||
                 (x.OrderId == orderId.Value && x.Status == PaymentStatus.Pending)),
                cancellationToken);

        if (payment is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.NotFound, "Pago local no encontrado para la notificacion.");
        }

        if (payment.ProviderPaymentId is null)
        {
            payment.ProviderPaymentId = providerPayment.ProviderPaymentId;
        }

        if (payment.Amount != providerPayment.Amount || !string.Equals(payment.Currency, providerPayment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "El monto o la moneda del pago no coinciden con la orden.");
        }

        var status = PaymentStatusMapper.FromProviderStatus(providerPayment.Status);
        if (status is null)
        {
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        return await PaymentStatusApplier.ApplyAsync(_db, payment, status.Value, providerPayment.ProviderPaymentId, providerPayment.FailureReason, cancellationToken);
    }
}

internal static class PaymentQueries
{
    public static Task<Payment?> LoadTrackedPaymentAsync(IApplicationDbContext db, int id, CancellationToken cancellationToken)
    {
        return db.Payments
            .Include(x => x.Order)
            .ThenInclude(x => x.Items)
            .ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}

internal static class PaymentStatusApplier
{
    public static async Task<AppResult<PaymentResponse>> ApplyAsync(
        IApplicationDbContext db,
        Payment payment,
        PaymentStatus newStatus,
        string? providerPaymentId,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        if (payment.Status != PaymentStatus.Pending)
        {
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        if (newStatus == PaymentStatus.Pending)
        {
            payment.ProviderPaymentId = string.IsNullOrWhiteSpace(providerPaymentId) ? payment.ProviderPaymentId : providerPaymentId.Trim();
            payment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        if (payment.Order.Status != OrderStatus.Pending)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "El pedido ya no esta pendiente de pago.");
        }

        payment.Status = newStatus;
        payment.ProviderPaymentId = string.IsNullOrWhiteSpace(providerPaymentId) ? payment.ProviderPaymentId : providerPaymentId.Trim();
        payment.FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        payment.UpdatedAt = DateTime.UtcNow;

        if (newStatus == PaymentStatus.Approved)
        {
            payment.PaidAt = DateTime.UtcNow;
            payment.Order.Status = OrderStatus.Paid;
            payment.Order.UpdatedAt = DateTime.UtcNow;
        }
        else if (newStatus is PaymentStatus.Rejected or PaymentStatus.Canceled or PaymentStatus.Expired)
        {
            payment.Order.Status = OrderStatus.Canceled;
            payment.Order.UpdatedAt = DateTime.UtcNow;

            foreach (var item in payment.Order.Items)
            {
                item.Product.Stock += item.Quantity;
                item.Product.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "Solo se puede resolver un pago pendiente como Approved, Rejected, Canceled o Expired.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
    }
}

internal static class PaymentStatusMapper
{
    public static PaymentStatus? FromProviderStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "approved" => PaymentStatus.Approved,
            "rejected" => PaymentStatus.Rejected,
            "cancelled" => PaymentStatus.Canceled,
            "canceled" => PaymentStatus.Canceled,
            "expired" => PaymentStatus.Expired,
            "refunded" => PaymentStatus.Refunded,
            "pending" => PaymentStatus.Pending,
            "in_process" => PaymentStatus.Pending,
            "in_mediation" => PaymentStatus.Pending,
            _ => null
        };
    }
}

internal static class PaymentExternalReferences
{
    public static int? TryGetOrderId(string externalReference)
    {
        const string prefix = "order-";
        return externalReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(externalReference[prefix.Length..], out var orderId)
            ? orderId
            : null;
    }
}

internal static class PaymentMapper
{
    public static PaymentResponse ToResponse(Payment payment)
    {
        return new PaymentResponse(
            payment.Id,
            payment.OrderId,
            payment.Provider,
            payment.ExternalReference,
            payment.ProviderPreferenceId,
            payment.ProviderPaymentId,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.CheckoutUrl,
            payment.FailureReason,
            payment.CreatedAt,
            payment.UpdatedAt,
            payment.PaidAt
        );
    }
}
