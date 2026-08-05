using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Carts;
using GymShop.Application.DTOs.Orders;
using GymShop.Application.UseCases.Orders;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Carts;

public interface IGetCartUseCase
{
    Task<CartResponse> ExecuteAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IAddCartItemUseCase
{
    Task<AppResult<CartResponse>> ExecuteAsync(int userId, AddCartItemRequest request, CancellationToken cancellationToken = default);
}

public interface IUpdateCartItemUseCase
{
    Task<AppResult<CartResponse>> ExecuteAsync(int userId, int productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);
}

public interface IRemoveCartItemUseCase
{
    Task<AppResult<CartResponse>> ExecuteAsync(int userId, int productId, CancellationToken cancellationToken = default);
}

public interface IClearCartUseCase
{
    Task<AppResult> ExecuteAsync(int userId, CancellationToken cancellationToken = default);
}

public interface ICheckoutCartUseCase
{
    Task<AppResult<OrderResponse>> ExecuteAsync(int userId, CheckoutCartRequest request, CancellationToken cancellationToken = default);
}

public class GetCartUseCase : IGetCartUseCase
{
    private readonly IApplicationDbContext _db;

    public GetCartUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CartResponse> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cart = await CartQueries.GetOrCreateCartAsync(_db, userId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await CartQueries.LoadCartResponseAsync(_db, cart.Id, cancellationToken);
    }
}

public class AddCartItemUseCase : IAddCartItemUseCase
{
    private readonly IApplicationDbContext _db;

    public AddCartItemUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<CartResponse>> ExecuteAsync(int userId, AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProductId <= 0 || request.Quantity <= 0)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.Validation, "Producto y cantidad son obligatorios.");
        }

        if (await CartQueries.HasPendingOrderAsync(_db, userId, cancellationToken))
        {
            return AppResult<CartResponse>.Failure(AppErrorType.Conflict, "Ya tenes una orden pendiente. Pagala o cancelala antes de modificar el carrito.");
        }

        var product = await _db.Products.SingleOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken);
        if (product is null || !product.IsActive)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.NotFound, "Producto no encontrado o inactivo.");
        }

        var cart = await CartQueries.GetOrCreateCartAsync(_db, userId, cancellationToken);
        var existingItem = await _db.CartItems.SingleOrDefaultAsync(x => x.CartId == cart.Id && x.ProductId == request.ProductId, cancellationToken);
        var newQuantity = (existingItem?.Quantity ?? 0) + request.Quantity;

        if (product.Stock < newQuantity)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.Validation, $"No hay stock suficiente para {product.Name}.");
        }

        if (existingItem is null)
        {
            _db.CartItems.Add(new CartItem
            {
                Cart = cart,
                ProductId = product.Id,
                Quantity = request.Quantity
            });
        }
        else
        {
            existingItem.Quantity = newQuantity;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return AppResult<CartResponse>.Success(await CartQueries.LoadCartResponseAsync(_db, cart.Id, cancellationToken));
    }
}

public class UpdateCartItemUseCase : IUpdateCartItemUseCase
{
    private readonly IApplicationDbContext _db;

    public UpdateCartItemUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<CartResponse>> ExecuteAsync(int userId, int productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || request.Quantity <= 0)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.Validation, "Producto y cantidad son obligatorios.");
        }

        if (await CartQueries.HasPendingOrderAsync(_db, userId, cancellationToken))
        {
            return AppResult<CartResponse>.Failure(AppErrorType.Conflict, "Ya tenes una orden pendiente. Pagala o cancelala antes de modificar el carrito.");
        }

        var cart = await CartQueries.GetUserCartAsync(_db, userId, cancellationToken);
        if (cart is null)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.NotFound, "Carrito no encontrado.");
        }

        var item = await _db.CartItems.Include(x => x.Product).SingleOrDefaultAsync(x => x.CartId == cart.Id && x.ProductId == productId, cancellationToken);
        if (item is null)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.NotFound, "Producto no encontrado en el carrito.");
        }

        if (!item.Product.IsActive || item.Product.Stock < request.Quantity)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.Validation, $"No hay stock suficiente para {item.Product.Name}.");
        }

        item.Quantity = request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult<CartResponse>.Success(await CartQueries.LoadCartResponseAsync(_db, cart.Id, cancellationToken));
    }
}

public class RemoveCartItemUseCase : IRemoveCartItemUseCase
{
    private readonly IApplicationDbContext _db;

    public RemoveCartItemUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<CartResponse>> ExecuteAsync(int userId, int productId, CancellationToken cancellationToken = default)
    {
        if (await CartQueries.HasPendingOrderAsync(_db, userId, cancellationToken))
        {
            return AppResult<CartResponse>.Failure(AppErrorType.Conflict, "Ya tenes una orden pendiente. Pagala o cancelala antes de modificar el carrito.");
        }

        var cart = await CartQueries.GetUserCartAsync(_db, userId, cancellationToken);
        if (cart is null)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.NotFound, "Carrito no encontrado.");
        }

        var item = await _db.CartItems.SingleOrDefaultAsync(x => x.CartId == cart.Id && x.ProductId == productId, cancellationToken);
        if (item is null)
        {
            return AppResult<CartResponse>.Failure(AppErrorType.NotFound, "Producto no encontrado en el carrito.");
        }

        _db.CartItems.Remove(item);
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult<CartResponse>.Success(await CartQueries.LoadCartResponseAsync(_db, cart.Id, cancellationToken));
    }
}

public class ClearCartUseCase : IClearCartUseCase
{
    private readonly IApplicationDbContext _db;

    public ClearCartUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult> ExecuteAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (await CartQueries.HasPendingOrderAsync(_db, userId, cancellationToken))
        {
            return AppResult<CartResponse>.Failure(AppErrorType.Conflict, "Ya tenes una orden pendiente. Pagala o cancelala antes de modificar el carrito.");
        }

        var cart = await CartQueries.GetUserCartAsync(_db, userId, cancellationToken);
        if (cart is null)
        {
            return AppResult.Success();
        }

        var items = await _db.CartItems.Where(x => x.CartId == cart.Id).ToListAsync(cancellationToken);
        _db.CartItems.RemoveRange(items);
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult.Success();
    }
}

public class CheckoutCartUseCase : ICheckoutCartUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly ITransactionManager? _transactionManager;

    public CheckoutCartUseCase(IApplicationDbContext db, ITransactionManager? transactionManager = null)
    {
        _db = db;
        _transactionManager = transactionManager;
    }

    public async Task<AppResult<OrderResponse>> ExecuteAsync(int userId, CheckoutCartRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ShippingAddress))
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Validation, "La direccion de envio es obligatoria.");
        }

        if (request.ShippingAddress.Trim().Length > ValidationLimits.ShippingAddress)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Validation, "La direccion de envio no puede superar los 300 caracteres.");
        }

        var cart = await _db.Carts
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Validation, "El carrito esta vacio.");
        }

        var hasPendingOrder = await CartQueries.HasPendingOrderAsync(_db, userId, cancellationToken);
        if (hasPendingOrder)
        {
            return AppResult<OrderResponse>.Failure(AppErrorType.Conflict, "Ya tenes una orden pendiente. Pagala o cancelala antes de crear otra.");
        }

        var productIds = cart.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var item in cart.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsActive)
            {
                return AppResult<OrderResponse>.Failure(AppErrorType.Validation, "Uno de los productos del carrito no existe o no esta activo.");
            }

            if (product.Stock < item.Quantity)
            {
                return AppResult<OrderResponse>.Failure(AppErrorType.Validation, $"No hay stock suficiente para {product.Name}.");
            }
        }

        var orderLines = cart.Items
            .OrderBy(x => x.Id)
            .Select(item =>
            {
                var product = products[item.ProductId];
                var unitPrice = product.Price;

                return new
                {
                    Product = product,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = unitPrice,
                    item.Quantity,
                    Subtotal = unitPrice * item.Quantity
                };
            })
            .ToList();

        await using var transaction = await BeginTransactionAsync(cancellationToken);

        var order = new Order
        {
            UserId = userId,
            ShippingAddress = request.ShippingAddress.Trim(),
            Status = OrderStatus.Pending
        };

        foreach (var line in orderLines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                Subtotal = line.Subtotal
            });

            order.Total += line.Subtotal;
            line.Product.Stock -= line.Quantity;
            line.Product.UpdatedAt = DateTime.UtcNow;
        }

        _db.Orders.Add(order);
        _db.CartItems.RemoveRange(cart.Items);
        cart.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return AppResult<OrderResponse>.Success(await OrderQueries.LoadOrderResponseAsync(_db, order.Id, cancellationToken));
    }

    private async Task<IApplicationTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _transactionManager is null
            ? null
            : await _transactionManager.BeginTransactionAsync(cancellationToken);
    }
}

internal static class CartQueries
{
    public static async Task<Cart> GetOrCreateCartAsync(IApplicationDbContext db, int userId, CancellationToken cancellationToken)
    {
        var cart = await GetUserCartAsync(db, userId, cancellationToken);
        if (cart is not null)
        {
            return cart;
        }

        cart = new Cart { UserId = userId };
        db.Carts.Add(cart);
        return cart;
    }

    public static Task<bool> HasPendingOrderAsync(IApplicationDbContext db, int userId, CancellationToken cancellationToken)
    {
        return db.Orders.AnyAsync(x => x.UserId == userId && x.Status == OrderStatus.Pending, cancellationToken);
    }

    public static Task<Cart?> GetUserCartAsync(IApplicationDbContext db, int userId, CancellationToken cancellationToken)
    {
        return db.Carts
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public static async Task<CartResponse> LoadCartResponseAsync(IApplicationDbContext db, int cartId, CancellationToken cancellationToken)
    {
        var cart = await db.Carts
            .AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(x => x.Product)
            .SingleAsync(x => x.Id == cartId, cancellationToken);

        var items = cart.Items
            .OrderBy(x => x.Id)
            .Select(x => new CartItemResponse(
                x.ProductId,
                x.Product.Name,
                x.Product.Price,
                x.Quantity,
                x.Product.Price * x.Quantity,
                x.Product.Stock,
                x.Product.ImageUrl
            ))
            .ToList();

        return new CartResponse(cart.Id, cart.UserId, items.Sum(x => x.Subtotal), items);
    }
}
