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
        var all = await useCase.ExecuteAsync(new OrderFilterRequest());
        var filtered = await useCase.ExecuteAsync(new OrderFilterRequest(Search: "cliente-b"));

        Assert.True(all.IsSuccess);
        Assert.Equal(2, all.Value?.TotalItems);
        Assert.True(filtered.IsSuccess);
        Assert.Single(filtered.Value!.Items);
        Assert.Equal(orderB.Id, filtered.Value.Items[0].Id);
    }

    [Fact]
    public async Task GetOrders_paginates_and_filters_by_number_name_status_and_dates()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "ana@test.com");
        user.Name = "Ana";
        user.LastName = "Gimenez";
        var older = await SeedOrderAsync(db, user.Id, 5, 1, 100);
        older.CreatedAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var newer = await SeedOrderAsync(db, user.Id, 5, 1, 200);
        newer.CreatedAt = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        newer.Status = OrderStatus.Paid;
        await db.SaveChangesAsync();
        var useCase = new GetOrdersUseCase(db);

        var page = await useCase.ExecuteAsync(new OrderFilterRequest(Page: 1, PageSize: 1));
        var byName = await useCase.ExecuteAsync(new OrderFilterRequest(Search: "gimenez"));
        var byNumber = await useCase.ExecuteAsync(new OrderFilterRequest(Search: $"#{newer.Id}"));
        var filtered = await useCase.ExecuteAsync(new OrderFilterRequest(Status: "Paid", FromUtc: new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(newer.Id, Assert.Single(page.Value!.Items).Id);
        Assert.Equal(2, page.Value.TotalPages);
        Assert.Equal(2, byName.Value!.TotalItems);
        Assert.Equal(newer.Id, Assert.Single(byNumber.Value!.Items).Id);
        Assert.Equal(newer.Id, Assert.Single(filtered.Value!.Items).Id);
    }

    [Fact]
    public async Task GetOrderById_returns_not_found_for_missing_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var result = await new GetOrderByIdUseCase(db).ExecuteAsync(999, 1, canViewAll: true);
        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task UpdateOrderStatus_advances_fulfillment_and_audits_each_change()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "cliente@test.com");
        var order = await SeedOrderAsync(db, user.Id, 5, 1, 100);
        order.Status = OrderStatus.Paid;
        await db.SaveChangesAsync();
        var useCase = new UpdateOrderStatusUseCase(db);

        Assert.True((await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Preparing"))).IsSuccess);
        Assert.True((await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Shipped", order.UpdatedAt, "Correo Argentino", "TRACK-1", "https://correo.example/track/TRACK-1"))).IsSuccess);
        Assert.True((await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Delivered", order.UpdatedAt))).IsSuccess);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(3, await db.AuditEntries.CountAsync());
        Assert.All(db.AuditEntries, entry => Assert.Equal("OrderStatusChanged", entry.Action));
    }

    [Fact]
    public async Task Shipping_home_delivery_requires_valid_tracking_and_allows_correction()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "tracking@test.com");
        var order = await SeedOrderAsync(db, user.Id, 5, 1, 100);
        order.Status = OrderStatus.Preparing;
        await db.SaveChangesAsync();
        var useCase = new UpdateOrderStatusUseCase(db);

        var missing = await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Shipped", order.UpdatedAt));
        var invalidUrl = await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Shipped", order.UpdatedAt, "Andreani", "ABC", "http://inseguro.test"));
        var shipped = await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Shipped", order.UpdatedAt, "Andreani", "ABC", "https://tracking.example/ABC"));
        var corrected = await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Shipped", order.UpdatedAt, "Andreani", "XYZ", "https://tracking.example/XYZ"));

        Assert.Equal(AppErrorType.Validation, missing.Error?.Type);
        Assert.Equal(AppErrorType.Validation, invalidUrl.Error?.Type);
        Assert.True(shipped.IsSuccess);
        Assert.True(corrected.IsSuccess);
        Assert.Equal("XYZ", order.TrackingNumber);
        Assert.Contains(db.AuditEntries, x => x.Action == "OrderTrackingUpdated");
    }

    [Fact]
    public async Task CancelOrder_cancels_pending_and_restores_stock_once()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "cliente@test.com");
        var order = await SeedOrderAsync(db, user.Id, stock: 5, quantity: 2, price: 100);
        var product = await db.Products.SingleAsync();
        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = "Mock",
            ExternalReference = $"order-{order.Id}",
            Amount = order.Total,
            Status = PaymentStatus.Creating
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        Assert.Equal(3, product.Stock);

        var useCase = new CancelOrderUseCase(db);
        var first = await useCase.ExecuteAsync(order.Id, user.Id, false, new CancelOrderRequest("Cliente no pago"));
        var second = await useCase.ExecuteAsync(order.Id, user.Id, false, new CancelOrderRequest("Reintento"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Equal(PaymentStatus.Canceled, payment.Status);
        Assert.Equal("Cliente no pago", payment.FailureReason);
        Assert.Equal("Cliente no pago", order.CancellationReason);
        Assert.Equal(5, product.Stock);
        Assert.Equal("OrderCanceled", Assert.Single(db.AuditEntries).Action);
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
    public async Task CancelOrder_persists_first_reason_even_without_payment()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "sin-pago@test.com");
        var order = await SeedOrderAsync(db, user.Id, stock: 5, quantity: 1, price: 100);
        var useCase = new CancelOrderUseCase(db);

        var first = await useCase.ExecuteAsync(order.Id, user.Id, false, new CancelOrderRequest("Cambio de decision"));
        var repeated = await useCase.ExecuteAsync(order.Id, user.Id, false, new CancelOrderRequest("No reemplazar"));

        Assert.True(first.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal("Cambio de decision", order.CancellationReason);
        Assert.Equal("Cambio de decision", repeated.Value?.CancellationReason);
        Assert.Empty(order.Payments);
        Assert.Equal("OrderCanceled", Assert.Single(db.AuditEntries).Action);
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
        var audit = Assert.Single(db.AuditEntries);
        Assert.Equal("OrderExpiredAdministratively", audit.Action);
        Assert.Equal(oldPending.Id.ToString(), audit.EntityId);
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
        Assert.Equal(AppErrorType.Conflict, result.Error?.Type);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Empty(db.AuditEntries);
    }

    [Fact]
    public async Task Admin_cancel_pending_restores_stock_and_cancels_pending_payment_once()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "cliente@test.com");
        var order = await SeedOrderAsync(db, user.Id, stock: 5, quantity: 2, price: 100);
        var product = await db.Products.SingleAsync();
        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = "Mock",
            ExternalReference = $"order-{order.Id}",
            Amount = order.Total,
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        var useCase = new UpdateOrderStatusUseCase(db);

        var first = await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Canceled"));
        var second = await useCase.ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Canceled"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.Equal(PaymentStatus.Canceled, payment.Status);
        Assert.Equal("Cancelacion administrativa del pedido.", payment.FailureReason);
        Assert.Equal(5, product.Stock);
        Assert.Equal("OrderStatusChanged", Assert.Single(db.AuditEntries).Action);
    }

    [Fact]
    public async Task Admin_generic_status_cannot_cancel_paid_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db, "cliente@test.com");
        var order = await SeedOrderAsync(db, user.Id, stock: 5, quantity: 1, price: 100);
        order.Status = OrderStatus.Paid;
        await db.SaveChangesAsync();

        var result = await new UpdateOrderStatusUseCase(db)
            .ExecuteAsync(order.Id, new UpdateOrderStatusRequest("Canceled"));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Conflict, result.Error?.Type);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(4, db.Products.Single().Stock);
        Assert.Empty(db.AuditEntries);
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
