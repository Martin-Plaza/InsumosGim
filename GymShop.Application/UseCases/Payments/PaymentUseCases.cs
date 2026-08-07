using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Payments;
using GymShop.Application.UseCases.Orders;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Payments;

public interface ICreatePaymentUseCase
{
    Task<AppResult<PaymentResponse>> ExecuteAsync(int orderId, int userId, bool canManageAll, CreatePaymentRequest request, CancellationToken cancellationToken = default);
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

public sealed class PaymentCreationPolicy
{
    public static PaymentCreationPolicy Default { get; } = FromSeconds(300);

    private PaymentCreationPolicy(TimeSpan creatingTimeout)
    {
        CreatingTimeout = creatingTimeout;
    }

    public TimeSpan CreatingTimeout { get; }

    public static PaymentCreationPolicy FromSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), "The payment creation timeout must be greater than zero.");
        }

        return new PaymentCreationPolicy(TimeSpan.FromSeconds(seconds));
    }
}

public class CreatePaymentUseCase : ICreatePaymentUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly PaymentCreationPolicy _policy;

    public CreatePaymentUseCase(IApplicationDbContext db, IEnumerable<IPaymentGateway> gateways)
        : this(db, gateways, PaymentCreationPolicy.Default)
    {
    }

    public CreatePaymentUseCase(
        IApplicationDbContext db,
        IEnumerable<IPaymentGateway> gateways,
        PaymentCreationPolicy policy)
    {
        _db = db;
        _gateways = gateways;
        _policy = policy;
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

        return await PaymentCreator.CreateAsync(_db, _gateways, order, request, _policy, cancellationToken);
    }
}

internal static class PaymentCreator
{
    public static async Task<AppResult<PaymentResponse>> CreateAsync(
        IApplicationDbContext db,
        IEnumerable<IPaymentGateway> gateways,
        Order order,
        CreatePaymentRequest request,
        PaymentCreationPolicy policy,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"server-{Guid.NewGuid():N}"
            : request.IdempotencyKey.Trim();
        if (idempotencyKey.Length > ValidationLimits.IdempotencyKey)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "La clave de idempotencia no puede superar 100 caracteres.");
        }

        var idempotentPayment = await db.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (idempotentPayment is not null)
        {
            if (idempotentPayment.OrderId != order.Id)
            {
                return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "La clave de idempotencia ya fue usada en otra orden.");
            }

            return await ResumeOrReuseAsync(db, gateways, order, idempotentPayment, policy, cancellationToken);
        }

        var activePayment = await db.Payments
            .AsNoTracking()
            .Where(x => x.OrderId == order.Id &&
                        (x.Status == PaymentStatus.Creating || x.Status == PaymentStatus.Pending))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (activePayment is not null)
        {
            return await ResumeOrReuseAsync(db, gateways, order, activePayment, policy, cancellationToken);
        }

        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "Mock" : request.Provider.Trim();
        if (provider.Length > ValidationLimits.PaymentProvider)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "El proveedor no puede superar 50 caracteres.");
        }

        var gateway = gateways.FirstOrDefault(x => x.CanHandle(provider));
        if (gateway is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, $"Proveedor de pago no soportado: {provider}.");
        }

        var now = DateTime.UtcNow;
        var reservation = new Payment
        {
            OrderId = order.Id,
            Provider = provider,
            ExternalReference = $"order-{order.Id}",
            IdempotencyKey = idempotencyKey,
            Amount = order.Total,
            Currency = "ARS",
            Status = PaymentStatus.Creating,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Payments.Add(reservation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Payments.Remove(reservation);

            var winner = await db.Payments
                .AsNoTracking()
                .Where(x => x.IdempotencyKey == idempotencyKey ||
                            (x.OrderId == order.Id &&
                             (x.Status == PaymentStatus.Creating || x.Status == PaymentStatus.Pending)))
                .OrderByDescending(x => x.IdempotencyKey == idempotencyKey)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (winner is null)
            {
                throw;
            }

            if (winner.IdempotencyKey == idempotencyKey && winner.OrderId != order.Id)
            {
                return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "La clave de idempotencia ya fue usada en otra orden.");
            }

            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(winner));
        }

        return await CompleteReservationAsync(db, gateway, order, reservation, cancellationToken);
    }

    private static async Task<AppResult<PaymentResponse>> ResumeOrReuseAsync(
        IApplicationDbContext db,
        IEnumerable<IPaymentGateway> gateways,
        Order order,
        Payment payment,
        PaymentCreationPolicy policy,
        CancellationToken cancellationToken)
    {
        if (payment.Status != PaymentStatus.Creating)
        {
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        var lastActivity = payment.UpdatedAt ?? payment.CreatedAt;
        var staleBefore = DateTime.UtcNow.Subtract(policy.CreatingTimeout);
        if (lastActivity > staleBefore)
        {
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        var claimedAt = DateTime.UtcNow;
        var claimed = await db.Payments
            .Where(x => x.Id == payment.Id &&
                        x.Status == PaymentStatus.Creating &&
                        (x.UpdatedAt ?? x.CreatedAt) <= staleBefore)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.UpdatedAt, claimedAt),
                cancellationToken);

        if (claimed == 0)
        {
            var current = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == payment.Id, cancellationToken);
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(current));
        }

        var reservation = await db.Payments.SingleAsync(x => x.Id == payment.Id, cancellationToken);
        var gateway = gateways.FirstOrDefault(x => x.CanHandle(reservation.Provider));
        if (gateway is null)
        {
            reservation.Status = PaymentStatus.CreationFailed;
            reservation.FailureReason = $"Proveedor de pago no soportado: {reservation.Provider}.";
            reservation.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, reservation.FailureReason);
        }

        return await CompleteReservationAsync(db, gateway, order, reservation, cancellationToken);
    }

    private static async Task<AppResult<PaymentResponse>> CompleteReservationAsync(
        IApplicationDbContext db,
        IPaymentGateway gateway,
        Order order,
        Payment reservation,
        CancellationToken cancellationToken)
    {
        PaymentPreferenceResult preference;
        try
        {
            preference = await gateway.CreatePreferenceAsync(order, reservation.IdempotencyKey, cancellationToken);
        }
        catch (PaymentGatewayException ex)
        {
            var stillCreating = await IsReservationStillCreatingForPendingOrderAsync(db, reservation.Id, cancellationToken);
            if (!stillCreating)
            {
                var current = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == reservation.Id, cancellationToken);
                return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(current));
            }

            reservation.Status = PaymentStatus.CreationFailed;
            reservation.FailureReason = ex.Message;
            reservation.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, ex.Message);
        }

        var canComplete = await IsReservationStillCreatingForPendingOrderAsync(db, reservation.Id, cancellationToken);
        if (!canComplete)
        {
            var current = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == reservation.Id, cancellationToken);
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(current));
        }

        reservation.Provider = preference.Provider;
        reservation.ProviderPreferenceId = preference.ProviderPreferenceId;
        reservation.Status = PaymentStatus.Pending;
        reservation.CheckoutUrl = preference.CheckoutUrl;
        reservation.FailureReason = null;
        reservation.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(reservation));
    }

    private static Task<bool> IsReservationStillCreatingForPendingOrderAsync(
        IApplicationDbContext db,
        int paymentId,
        CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking().AnyAsync(
            x => x.Id == paymentId &&
                 x.Status == PaymentStatus.Creating &&
                 x.Order.Status == OrderStatus.Pending,
            cancellationToken);
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
    private readonly IAuditContext? _auditContext;

    public UpdatePaymentStatusUseCase(IApplicationDbContext db, IAuditContext? auditContext = null)
    {
        _db = db;
        _auditContext = auditContext;
    }

    public async Task<AppResult<PaymentResponse>> ExecuteAsync(int id, UpdatePaymentStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProviderPaymentId?.Trim().Length > ValidationLimits.PaymentProviderId)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "El identificador del proveedor no puede superar 100 caracteres.");
        }

        if (request.FailureReason?.Trim().Length > ValidationLimits.PaymentFailureReason)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "El motivo no puede superar 500 caracteres.");
        }

        if (!Enum.TryParse<PaymentStatus>(request.Status, true, out var newStatus) || !Enum.IsDefined(newStatus))
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "Estado de pago invalido.");
        }

        var payment = await PaymentQueries.LoadTrackedPaymentAsync(_db, id, cancellationToken);
        if (payment is null)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.NotFound, "Pago no encontrado.");
        }

        return await PaymentStatusApplier.ApplyAsync(
            _db,
            payment,
            newStatus,
            request.ProviderPaymentId,
            request.FailureReason,
            isProviderNotification: false,
            _auditContext,
            cancellationToken);
    }
}

public class HandlePaymentWebhookUseCase : IHandlePaymentWebhookUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly IAuditContext? _auditContext;

    public HandlePaymentWebhookUseCase(IApplicationDbContext db, IEnumerable<IPaymentGateway> gateways, IAuditContext? auditContext = null)
    {
        _db = db;
        _gateways = gateways;
        _auditContext = auditContext;
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

        if (PaymentStatusMapper.IsPartialRefund(providerPayment.Status))
        {
            if (payment.Status != PaymentStatus.Approved ||
                payment.Order.Status is not (OrderStatus.Paid or OrderStatus.Shipped))
            {
                return AppResult<PaymentResponse>.Failure(
                    AppErrorType.Conflict,
                    "El pago y el pedido no se encuentran en un estado compatible con un reembolso parcial.");
            }

            payment.FailureReason = "Reembolso parcial informado por el proveedor; requiere gestion manual.";
            payment.UpdatedAt = DateTime.UtcNow;
            AuditTrail.Add(_db, _auditContext, "PaymentPartialRefundFlagged", "Payment", payment.Id,
                new { status = payment.Status.ToString(), failureReason = (string?)null },
                new { status = payment.Status.ToString(), payment.FailureReason }, payment.FailureReason);
            await _db.SaveChangesAsync(cancellationToken);
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        var status = PaymentStatusMapper.FromProviderStatus(providerPayment.Status);
        if (status is null)
        {
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        return await PaymentStatusApplier.ApplyAsync(
            _db,
            payment,
            status.Value,
            providerPayment.ProviderPaymentId,
            providerPayment.FailureReason,
            isProviderNotification: true,
            _auditContext,
            cancellationToken);
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
        bool isProviderNotification,
        IAuditContext? auditContext,
        CancellationToken cancellationToken)
    {
        if (payment.Status == newStatus)
        {
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        if (newStatus == PaymentStatus.Refunded)
        {
            if (!isProviderNotification)
            {
                return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "Un reembolso solo puede aplicarse despues de ser confirmado por el proveedor.");
            }

            if (payment.Status != PaymentStatus.Approved ||
                payment.Order.Status is not (OrderStatus.Paid or OrderStatus.Shipped))
            {
                return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "El pago y el pedido no se encuentran en un estado reembolsable.");
            }

            var oldPaymentStatus = payment.Status;
            var oldOrderStatus = payment.Order.Status;
            var wasShipped = payment.Order.Status == OrderStatus.Shipped;
            OrderCompensation.RefundAndRestoreStockIfNotShipped(payment.Order);
            payment.Status = PaymentStatus.Refunded;
            payment.ProviderPaymentId = NormalizeProviderPaymentId(payment.ProviderPaymentId, providerPaymentId);
            payment.FailureReason = string.IsNullOrWhiteSpace(failureReason)
                ? wasShipped
                    ? "Reembolso total confirmado despues del envio; devolucion y stock pendientes de gestion manual."
                    : "Reembolso total confirmado por el proveedor."
                : failureReason.Trim();
            payment.UpdatedAt = DateTime.UtcNow;
            AuditTrail.Add(db, auditContext, "PaymentRefundedByProvider", "Payment", payment.Id,
                new { paymentStatus = oldPaymentStatus.ToString(), orderStatus = oldOrderStatus.ToString() },
                new { paymentStatus = payment.Status.ToString(), orderStatus = payment.Order.Status.ToString() },
                payment.FailureReason);
            await db.SaveChangesAsync(cancellationToken);
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "Transicion de estado de pago invalida.");
        }

        if (newStatus == PaymentStatus.Pending)
        {
            payment.ProviderPaymentId = NormalizeProviderPaymentId(payment.ProviderPaymentId, providerPaymentId);
            payment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
        }

        if (payment.Order.Status != OrderStatus.Pending)
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Conflict, "El pedido ya no esta pendiente de pago.");
        }

        var previousPaymentStatus = payment.Status;
        var previousOrderStatus = payment.Order.Status;
        payment.Status = newStatus;
        payment.ProviderPaymentId = NormalizeProviderPaymentId(payment.ProviderPaymentId, providerPaymentId);
        payment.FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim();
        payment.UpdatedAt = DateTime.UtcNow;

        if (newStatus == PaymentStatus.Approved)
        {
            payment.FailureReason = null;
            payment.PaidAt = DateTime.UtcNow;
            payment.Order.Status = OrderStatus.Paid;
            payment.Order.UpdatedAt = DateTime.UtcNow;
        }
        else if (newStatus is PaymentStatus.Rejected or PaymentStatus.Canceled or PaymentStatus.Expired)
        {
            OrderCompensation.CancelPendingAndRestoreStock(
                payment.Order,
                payment.FailureReason ?? $"Pago resuelto como {newStatus}.");
        }
        else
        {
            return AppResult<PaymentResponse>.Failure(AppErrorType.Validation, "Solo se puede resolver un pago pendiente como Approved, Rejected, Canceled o Expired.");
        }

        AuditTrail.Add(db, auditContext,
            isProviderNotification ? "PaymentResolvedByProvider" : "PaymentResolvedManually",
            "Payment", payment.Id,
            new { paymentStatus = previousPaymentStatus.ToString(), orderStatus = previousOrderStatus.ToString() },
            new { paymentStatus = payment.Status.ToString(), orderStatus = payment.Order.Status.ToString() },
            payment.FailureReason);

        await db.SaveChangesAsync(cancellationToken);
        return AppResult<PaymentResponse>.Success(PaymentMapper.ToResponse(payment));
    }

    private static string? NormalizeProviderPaymentId(string? currentValue, string? newValue) =>
        string.IsNullOrWhiteSpace(newValue) ? currentValue : newValue.Trim();
}

internal static class PaymentStatusMapper
{
    public static bool IsPartialRefund(string status) =>
        string.Equals(status, "partially_refunded", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "partially-refunded", StringComparison.OrdinalIgnoreCase);

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
            payment.IdempotencyKey,
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
