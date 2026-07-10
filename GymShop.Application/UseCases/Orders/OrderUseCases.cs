using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Orders;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Orders;

public interface IGetMyOrdersUseCase
{
    Task<List<OrderSummaryResponse>> ExecuteAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IGetOrderByIdUseCase
{
    Task<AppResult<OrderResponse>> ExecuteAsync(int id, int userId, bool canViewAll, CancellationToken cancellationToken = default);
}

public interface IGetOrdersUseCase
{
    Task<List<OrderSummaryResponse>> ExecuteAsync(OrderFilterRequest filter, CancellationToken cancellationToken = default);
}

public interface ICancelOrderUseCase
{
    Task<AppResult<OrderResponse>> ExecuteAsync(int id, int userId, bool canManageAll, CancelOrderRequest request, CancellationToken cancellationToken = default);
}

public interface IExpirePendingOrdersUseCase
{
    Task<AppResult<ExpirePendingOrdersResponse>> ExecuteAsync(ExpirePendingOrdersRequest request, CancellationToken cancellationToken = default);
}

public interface IUpdateOrderStatusUseCase
{
    Task<AppResult> ExecuteAsync(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default);
}

public class GetMyOrdersUseCase : IGetMyOrdersUseCase
{
    private readonly IApplicationDbContext _db;

    public GetMyOrdersUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<OrderSummaryResponse>> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _db.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.User)
            .Include(x => x.Payments)
            .OrderByDescending(x => x.Id)
            .Select(x => OrderMapper.ToSummaryResponse(x, x.User.Email))
            .ToListAsync(cancellationToken);
    }
}

public class GetOrderByIdUseCase : IGetOrderByIdUseCase
{
    private readonly IApplicationDbContext _db;

    public GetOrderByIdUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<OrderResponse>> ExecuteAsync(int id, int userId, bool canViewAll, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.NotFound, "Pedido no encontrado.");
        }

        if (order.UserId != userId && !canViewAll)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Forbidden, "No tenes permisos para ver este pedido.");
        }

        return AppResult<OrderResponse>.Success(OrderMapper.ToResponse(order));
    }
}

public class GetOrdersUseCase : IGetOrdersUseCase
{
    private readonly IApplicationDbContext _db;

    public GetOrdersUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<OrderSummaryResponse>> ExecuteAsync(OrderFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Payments)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.UserEmail))
        {
            var email = filter.UserEmail.Trim();
            query = query.Where(x => x.User.Email.Contains(email));
        }

        return await query
            .OrderByDescending(x => x.Id)
            .Select(x => OrderMapper.ToSummaryResponse(x, x.User.Email))
            .ToListAsync(cancellationToken);
    }
}


public class CancelOrderUseCase : ICancelOrderUseCase
{
    private readonly IApplicationDbContext _db;

    public CancelOrderUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<OrderResponse>> ExecuteAsync(int id, int userId, bool canManageAll, CancelOrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = await _db.Orders
            .Include(x => x.User)
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.NotFound, "Pedido no encontrado.");
        }

        if (order.UserId != userId && !canManageAll)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Forbidden, "No tenes permisos para cancelar este pedido.");
        }

        if (order.Status == OrderStatus.Canceled)
        {
            return AppResult<OrderResponse>.Success(OrderMapper.ToResponse(order));
        }

        if (order.Status != OrderStatus.Pending)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Conflict, "Solo se pueden cancelar pedidos pendientes desde este flujo.");
        }

        OrderCancellation.CancelPendingAndRestoreStock(order);
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult<OrderResponse>.Success(OrderMapper.ToResponse(order));
    }
}

public class ExpirePendingOrdersUseCase : IExpirePendingOrdersUseCase
{
    private readonly IApplicationDbContext _db;

    public ExpirePendingOrdersUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<ExpirePendingOrdersResponse>> ExecuteAsync(ExpirePendingOrdersRequest request, CancellationToken cancellationToken = default)
    {
        if (request.OlderThanMinutes <= 0)
        {
            return AppResult<ExpirePendingOrdersResponse>.Failure(AppErrorType.Validation, "El vencimiento debe ser mayor a cero minutos.");
        }

        var cutoff = DateTime.UtcNow.AddMinutes(-request.OlderThanMinutes);
        var orders = await _db.Orders
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .Where(x => x.Status == OrderStatus.Pending && x.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            OrderCancellation.CancelPendingAndRestoreStock(order);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return AppResult<ExpirePendingOrdersResponse>.Success(new ExpirePendingOrdersResponse(orders.Count));
    }
}

internal static class OrderCancellation
{
    public static void CancelPendingAndRestoreStock(Order order)
    {
        if (order.Status != OrderStatus.Pending)
        {
            return;
        }

        order.Status = OrderStatus.Canceled;
        order.UpdatedAt = DateTime.UtcNow;

        foreach (var item in order.Items)
        {
            item.Product.Stock += item.Quantity;
            item.Product.UpdatedAt = DateTime.UtcNow;
        }
    }
}

public class UpdateOrderStatusUseCase : IUpdateOrderStatusUseCase
{
    private readonly IApplicationDbContext _db;

    public UpdateOrderStatusUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult> ExecuteAsync(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status))
        {
            return AppResult.Failure(AppErrorType.Validation, "Estado invalido.");
        }

        var order = await _db.Orders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null)
        {
            return AppResult.Failure(AppErrorType.NotFound, "Pedido no encontrado.");
        }

        if (!OrderStatusTransitions.CanAdminMove(order.Status, status))
        {
            return AppResult.Failure(AppErrorType.Validation, "Transicion de estado invalida. El estado Paid solo puede venir del flujo de pagos.");
        }

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult.Success();
    }
}

internal static class OrderStatusTransitions
{
    public static bool CanAdminMove(OrderStatus current, OrderStatus next)
    {
        if (current == next)
        {
            return true;
        }

        return (current, next) switch
        {
            (OrderStatus.Pending, OrderStatus.Canceled) => true,
            (OrderStatus.Paid, OrderStatus.Shipped) => true,
            (OrderStatus.Paid, OrderStatus.Canceled) => true,
            _ => false
        };
    }
}

internal static class OrderQueries
{
    public static async Task<OrderResponse> LoadOrderResponseAsync(IApplicationDbContext db, int id, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(x => x.User)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == id, cancellationToken);

        return OrderMapper.ToResponse(order);
    }
}

internal static class OrderMapper
{
    public static OrderResponse ToResponse(Order order)
    {
        return new OrderResponse(
            order.Id,
            order.UserId,
            order.User.Email,
            order.CreatedAt,
            order.Total,
            order.Status.ToString(),
            order.ShippingAddress,
            order.Items
                .OrderBy(x => x.Id)
                .Select(x => new OrderItemResponse(x.ProductId, x.ProductName, x.UnitPrice, x.Quantity, x.Subtotal))
                .ToList(),
            order.Payments
                .OrderByDescending(x => x.Id)
                .Select(x => new OrderPaymentResponse(x.Id, x.Provider, x.Amount, x.Currency, x.Status.ToString(), x.CreatedAt, x.PaidAt))
                .ToList()
        );
    }

    public static OrderSummaryResponse ToSummaryResponse(Order order, string? userEmail)
    {
        var lastPayment = order.Payments.OrderByDescending(x => x.Id).FirstOrDefault();

        return new OrderSummaryResponse(
            order.Id,
            order.UserId,
            userEmail,
            order.CreatedAt,
            order.Total,
            order.Status.ToString(),
            lastPayment?.Status.ToString(),
            lastPayment?.Id
        );
    }
}










