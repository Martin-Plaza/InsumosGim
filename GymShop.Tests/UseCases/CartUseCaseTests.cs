using GymShop.Application.Common;
using GymShop.Application.DTOs.Carts;
using GymShop.Application.UseCases.Carts;
using GymShop.Domain.Entities;
using GymShop.Infrastructure.Services;
using GymShop.Tests.TestSupport;

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
            Name = "Mancuerna",
            Description = "Mancuerna 10kg",
            Price = price,
            Stock = stock,
            IsActive = true
        };

        db.Products.Add(product);
        return product;
    }
}
