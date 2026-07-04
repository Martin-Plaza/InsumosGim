using GymShop.Application.Common;
using GymShop.Application.DTOs.Orders;
using GymShop.Application.UseCases.Orders;
using GymShop.Domain.Entities;
using GymShop.Infrastructure.Services;
using GymShop.Tests.TestSupport;

namespace GymShop.Tests.UseCases;

public class OrderUseCaseTests
{
    [Fact]
    public async Task CreateOrder_creates_order_items_and_decrements_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 5, price: 100);
        await db.SaveChangesAsync();

        var useCase = new CreateOrderUseCase(db);
        var result = await useCase.ExecuteAsync(
            user.Id,
            new CreateOrderRequest("Av. Siempre Viva 742", [new CreateOrderItemRequest(product.Id, 2)])
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(200, result.Value.Total);
        Assert.Single(result.Value.Items);
        Assert.Equal(3, product.Stock);
        Assert.Single(db.Orders);
        Assert.Single(db.OrderItems);
    }

    [Fact]
    public async Task CreateOrder_rejects_when_stock_is_insufficient()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = SeedProduct(db, stock: 1, price: 100);
        await db.SaveChangesAsync();

        var useCase = new CreateOrderUseCase(db);
        var result = await useCase.ExecuteAsync(
            user.Id,
            new CreateOrderRequest("Av. Siempre Viva 742", [new CreateOrderItemRequest(product.Id, 2)])
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Validation, result.Error?.Type);
        Assert.Empty(db.Orders);
        Assert.Equal(1, product.Stock);
    }

    private static async Task<User> SeedUserAsync(GymShop.Infrastructure.Data.GymShopDbContext db)
    {
        var role = db.Roles.Single(x => x.Name == "User");
        var user = new User
        {
            Email = "cliente@test.com",
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
