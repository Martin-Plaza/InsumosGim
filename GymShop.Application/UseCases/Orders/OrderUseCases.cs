using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Orders;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Orders;

public interface ICreateOrderUseCase
{
    Task<AppResult<OrderResponse>> ExecuteAsync(int userId, CreateOrderRequest request, CancellationToken cancellationToken = default);
}

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
    Task<List<OrderSummaryResponse>> ExecuteAsync(CancellationToken cancellationToken = default);
}

public interface IUpdateOrderStatusUseCase
{
    Task<AppResult> ExecuteAsync(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default);
}

public class CreateOrderUseCase : ICreateOrderUseCase
{
    private readonly IApplicationDbContext _db;

    public CreateOrderUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<OrderResponse>> ExecuteAsync(int userId, CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ShippingAddress))
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Validation, "La direccion de envio es obligatoria.");
        }

        if (request.Items.Count == 0)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Validation, "El pedido debe tener al menos un item.");
        }

        if (request.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Validation, "Todos los items deben tener producto y cantidad validos.");
        }

        var requestedItems = request.Items
            .GroupBy(x => x.ProductId)
            .Select(x => new CreateOrderItemRequest(x.Key, x.Sum(i => i.Quantity)))
            .ToList();

        var productIds = requestedItems.Select(x => x.ProductId).ToList();
        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var item in requestedItems)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
            {
                return AppResult<OrderResponse>.Failure(AppErrorType.Validation, $"El producto {item.ProductId} no existe o no esta activo.");
            }

            if (product.Stock < item.Quantity)
            {
                return AppResult<OrderResponse>.Failure(AppErrorType.Validation, $"No hay stock suficiente para {product.Name}.");
            }
        }

        var order = new Order
        {
            UserId = userId,
            ShippingAddress = request.ShippingAddress.Trim(),
            Status = OrderStatus.Pending
        };

        foreach (var item in requestedItems)
        {
            var product = products[item.ProductId];
            var subtotal = product.Price * item.Quantity;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                Subtotal = subtotal
            });

            order.Total += subtotal;
            product.Stock -= item.Quantity;
            product.UpdatedAt = DateTime.UtcNow;
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        var created = await OrderQueries.LoadOrderResponseAsync(_db, order.Id, cancellationToken);
        return AppResult<OrderResponse>.Success(created);
    }
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
            .OrderByDescending(x => x.Id)
            .Select(x => OrderMapper.ToSummaryResponse(x, null))
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

    public async Task<List<OrderSummaryResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Orders
            .AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.Id)
            .Select(x => OrderMapper.ToSummaryResponse(x, x.User.Email))
            .ToListAsync(cancellationToken);
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

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult.Success();
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
                .ToList()
        );
    }

    public static OrderSummaryResponse ToSummaryResponse(Order order, string? userEmail)
    {
        return new OrderSummaryResponse(
            order.Id,
            order.UserId,
            userEmail,
            order.CreatedAt,
            order.Total,
            order.Status.ToString()
        );
    }
}
