using GymShop.Application.Common;
using GymShop.Application.DTOs.Carts;
using GymShop.Application.UseCases.Carts;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using GymShop.Infrastructure.Services;
using GymShop.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Tests.UseCases;

public class CartUseCaseTests
{
    [Fact]
    public async Task AddCartItem_adds_product_and_accumulates_quantity()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 5, price: 100);
        await db.SaveChangesAsync();

        var useCase = new AddCartItemUseCase(db);
        var first = await useCase.ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 1));
        var second = await useCase.ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 2));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotNull(second.Value);
        Assert.Single(second.Value.Items);
        Assert.Equal(3, second.Value.Items[0].Quantity);
        Assert.Equal(300, second.Value.Total);
    }

    [Fact]
    public async Task AddCartItem_rejects_when_stock_is_insufficient()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 1, price: 100);
        await db.SaveChangesAsync();

        var useCase = new AddCartItemUseCase(db);
        var result = await useCase.ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 2));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Validation, result.Error?.Type);
        Assert.Empty(db.CartItems);
    }

    [Fact]
    public async Task AddCartItem_rejects_zero_quantity()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 5, price: 100);
        await db.SaveChangesAsync();

        var useCase = new AddCartItemUseCase(db);
        var result = await useCase.ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Validation, result.Error?.Type);
        Assert.Empty(db.CartItems);
    }

    [Fact]
    public async Task CheckoutCart_creates_order_decrements_stock_and_clears_cart()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 5, price: 100);
        await db.SaveChangesAsync();

        var addToCart = new AddCartItemUseCase(db);
        await addToCart.ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 2));

        var checkout = new CheckoutCartUseCase(db);
        var result = await checkout.ExecuteAsync(user.Id, new CheckoutCartRequest("Av. Siempre Viva 742"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(200, result.Value.Total);
        Assert.Single(result.Value.Items);
        Assert.Equal(3, product.Stock);
        Assert.Single(db.Orders);
        Assert.Single(db.OrderItems);
        Assert.Empty(db.CartItems);
    }

    [Fact]
    public async Task CheckoutCart_preserves_product_price_at_purchase_time()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 5, price: 100);
        await db.SaveChangesAsync();

        var addToCart = new AddCartItemUseCase(db);
        await addToCart.ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 2));

        var checkout = new CheckoutCartUseCase(db);
        var result = await checkout.ExecuteAsync(user.Id, new CheckoutCartRequest("Av. Siempre Viva 742"));
        product.Price = 999;
        await db.SaveChangesAsync();

        var item = await db.OrderItems.SingleAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal(100, item.UnitPrice);
        Assert.Equal(200, item.Subtotal);
    }

    [Fact]
    public async Task CheckoutCart_with_two_products_decrements_both_stocks()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var firstProduct = SeedProduct(db, stock: 5, price: 100);
        var secondProduct = SeedProduct(db, stock: 8, price: 50);
        await db.SaveChangesAsync();

        var addToCart = new AddCartItemUseCase(db);
        await addToCart.ExecuteAsync(user.Id, new AddCartItemRequest(firstProduct.Id, 2));
        await addToCart.ExecuteAsync(user.Id, new AddCartItemRequest(secondProduct.Id, 3));

        var checkout = new CheckoutCartUseCase(db);
        var result = await checkout.ExecuteAsync(user.Id, new CheckoutCartRequest("Av. Siempre Viva 742"));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, firstProduct.Stock);
        Assert.Equal(5, secondProduct.Stock);
        Assert.Equal(350, result.Value?.Total);
        Assert.Equal(2, db.OrderItems.Count());
    }

    [Fact]
    public async Task CheckoutCart_rejects_missing_product_without_changing_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 5, price: 100);
        var cart = new Cart { UserId = user.Id };
        cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1 });
        cart.Items.Add(new CartItem { ProductId = 999999, Quantity = 1 });
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var checkout = new CheckoutCartUseCase(db);
        var result = await checkout.ExecuteAsync(user.Id, new CheckoutCartRequest("Av. Siempre Viva 742"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Validation, result.Error?.Type);
        Assert.Equal(5, product.Stock);
        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task CheckoutCart_rejects_when_user_already_has_pending_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 5, price: 100);
        await db.SaveChangesAsync();

        var addToCart = new AddCartItemUseCase(db);
        await addToCart.ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 1));

        db.Orders.Add(new Order
        {
            UserId = user.Id,
            ShippingAddress = "Av. Siempre Viva 742",
            Status = OrderStatus.Pending,
            Total = 100
        });
        await db.SaveChangesAsync();

        var checkout = new CheckoutCartUseCase(db);
        var result = await checkout.ExecuteAsync(user.Id, new CheckoutCartRequest("Otra direccion 123"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Conflict, result.Error?.Type);
        Assert.Single(db.Orders);
    }

    [Fact]
    public async Task Pending_order_does_not_block_cart_mutations()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var firstProduct = SeedProduct(db, stock: 8, price: 100);
        var secondProduct = SeedProduct(db, stock: 6, price: 50);
        await db.SaveChangesAsync();

        var add = new AddCartItemUseCase(db);
        Assert.True((await add.ExecuteAsync(user.Id, new AddCartItemRequest(firstProduct.Id, 1))).IsSuccess);
        db.Orders.Add(new Order
        {
            UserId = user.Id,
            ShippingAddress = "Compra anterior 123",
            Status = OrderStatus.Pending,
            Total = 100
        });
        await db.SaveChangesAsync();

        var added = await add.ExecuteAsync(user.Id, new AddCartItemRequest(secondProduct.Id, 2));
        var updated = await new UpdateCartItemUseCase(db).ExecuteAsync(user.Id, firstProduct.Id, new UpdateCartItemRequest(3));
        var removed = await new RemoveCartItemUseCase(db).ExecuteAsync(user.Id, secondProduct.Id);
        var cleared = await new ClearCartUseCase(db).ExecuteAsync(user.Id);

        Assert.True(added.IsSuccess);
        Assert.True(updated.IsSuccess);
        Assert.True(removed.IsSuccess);
        Assert.True(cleared.IsSuccess);
        Assert.Empty(db.CartItems);
        Assert.Single(db.Orders);
        Assert.Equal(OrderStatus.Pending, db.Orders.Single().Status);
    }

    private static async Task<User> SeedUserAsync(GymShop.Infrastructure.Data.GymShopDbContext db)
    {
        var role = db.Roles.Single(x => x.Name == "User");
        var user = new User
        {
            Email = $"cliente-{Guid.NewGuid():N}@test.com",
            Name = "Cliente Test",
            PasswordHash = new PasswordHasher().Hash("123456"),
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static Product SeedProduct(GymShop.Infrastructure.Data.GymShopDbContext db, int stock, decimal price)
    {
        var product = new Product
        {
            Name = $"Producto {Guid.NewGuid():N}",
            Description = "Producto test",
            Price = price,
            Stock = stock,
            IsActive = true
        };

        db.Products.Add(product);
        return product;
    }
}
