using GymShop.Application.Common;
using GymShop.Application.DTOs.Orders;
using GymShop.Application.UseCases.Orders;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using GymShop.Infrastructure.Services;
using GymShop.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Tests.UseCases;

public class AdminOrderUseCaseTests
{
    [Fact]
    public async Task GetOrders_returns_all_or_filters_by_user_email()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var userA = await SeedUserAsync(db, "cliente-a@test.com");
        var userB = await SeedUserAsync(db, "cliente-b@test.com");
        var orderA = await SeedOrderAsync(db, userA.Id, stock: 5, quantity: 1, price: 100);
        var orderB = await SeedOrderAsync(db, userB.Id, stock: 5, quantity: 1, price: 200);
        orderB.Status = OrderStatus.Paid;
        await db.SaveChangesAsync();

        var useCase = new GetOrdersUseCase(db);
        var all = await useCase.ExecuteAsync(new OrderFilterRequest(null));
        var filtered = await useCase.ExecuteAsync(new OrderFilterRequest("cliente-b"));

        Assert.Equal(2, all.Count);
        Assert.Single(filtered);
        Assert.Equal(orderB.Id, filtered[0].Id);
    }

    [Fact]
    public async Task CancelOrder_cancels_pending_and_restores_stock_once()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "cliente@test.com");
        var order = await SeedOrderAsync(db, user.Id, stock: 5, quantity: 2, price: 100);
        var product = await db.Products.SingleAsync();

        Assert.Equal(3, product.Stock);

        var useCase = new CancelOrderUseCase(db);
        var first = await useCase.ExecuteAsync(order.Id, user.Id, false, new CancelOrderRequest("Cliente no pago"));
        var second = await useCase.ExecuteAsync(order.Id, user.Id, false, new CancelOrderRequest("Reintento"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Equal(5, product.Stock);
    }

    [Fact]
    public async Task CancelOrder_rejects_paid_orders()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "cliente@test.com");
        var order = await SeedOrderAsync(db, user.Id, stock: 5, quantity: 2, price: 100);
        order.Status = OrderStatus.Paid;
        await db.SaveChangesAsync();

        var useCase = new CancelOrderUseCase(db);
        var result = await useCase.ExecuteAsync(order.Id, user.Id, false, new CancelOrderRequest("No deberia"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Conflict, result.Error?.Type);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task ExpirePendingOrders_cancels_only_old_pending_and_restores_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "cliente@test.com");
        var oldPending = await SeedOrderAsync(db, user.Id, stock: 10, quantity: 2, price: 100);
        var recentPending = await SeedOrderAsync(db, user.Id, stock: 10, quantity: 2, price: 100);
        var oldPaid = await SeedOrderAsync(db, user.Id, stock: 10, quantity: 2, price: 100);
        oldPending.CreatedAt = DateTime.UtcNow.AddHours(-2);
        recentPending.CreatedAt = DateTime.UtcNow;
        oldPaid.CreatedAt = DateTime.UtcNow.AddHours(-2);
        oldPaid.Status = OrderStatus.Paid;
        await db.SaveChangesAsync();

        var useCase = new ExpirePendingOrdersUseCase(db);
        var result = await useCase.ExecuteAsync(new ExpirePendingOrdersRequest(60));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value?.CanceledOrders);
        Assert.Equal(OrderStatus.Canceled, oldPending.Status);
        Assert.Equal(OrderStatus.Pending, recentPending.Status);
        Assert.Equal(OrderStatus.Paid, oldPaid.Status);
    }

    [Fact]
    public async Task GetOrderById_rejects_other_user_and_allows_admin()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var owner = await SeedUserAsync(db, "owner@test.com");
        var otherUser = await SeedUserAsync(db, "other@test.com");
        var order = await SeedOrderAsync(db, owner.Id, stock: 5, quantity: 1, price: 100);

        var useCase = new GetOrderByIdUseCase(db);
        var otherUserResult = await useCase.ExecuteAsync(order.Id, otherUser.Id, canViewAll: false);
        var adminResult = await useCase.ExecuteAsync(order.Id, otherUser.Id, canViewAll: true);

        Assert.False(otherUserResult.IsSuccess);
        Assert.Equal(AppErrorType.Forbidden, otherUserResult.Error?.Type);
        Assert.True(adminResult.IsSuccess);
        Assert.Equal(order.Id, adminResult.Value?.Id);
    }

    [Fact]
    public async Task UpdateOrderStatus_rejects_invalid_transition()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "cliente@test.com");
        var order = await SeedOrderAsync(db, user.Id, stock: 5, quantity: 1, price: 100);

        var useCase = new UpdateOrderStatusUseCase(db);
        var result = await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Shipped"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Validation, result.Error?.Type);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }
    private static async Task<User> SeedUserAsync(GymShop.Infrastructure.Data.GymShopDbContext db, string email)
    {
        var role = db.Roles.Single(x => x.Name == "User");
        var user = new User
        {
            Email = email,
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

    private static async Task<Order> SeedOrderAsync(GymShop.Infrastructure.Data.GymShopDbContext db, int userId, int stock, int quantity, decimal price)
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
        await db.SaveChangesAsync();

        product.Stock -= quantity;
        var order = new Order
        {
            UserId = userId,
            ShippingAddress = "Av. Siempre Viva 742",
            Status = OrderStatus.Pending,
            Total = price * quantity
        };
        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = quantity,
            Subtotal = price * quantity,
            Product = product
        });

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return await db.Orders.Include(x => x.Items).ThenInclude(x => x.Product).SingleAsync(x => x.Id == order.Id);
    }
}
