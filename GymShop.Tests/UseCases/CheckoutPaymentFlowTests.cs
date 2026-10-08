using GymShop.Application.Common;
using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Carts;
using GymShop.Application.DTOs.Orders;
using GymShop.Application.DTOs.Payments;
using GymShop.Application.UseCases.Carts;
using GymShop.Application.UseCases.Orders;
using GymShop.Application.UseCases.Payments;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using GymShop.Infrastructure.Services;
using GymShop.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Tests.UseCases;

public class CheckoutPaymentFlowTests
{
    [Fact]
    public async Task Paid_checkout_creates_order_decrements_stock_and_preserves_new_cart_items()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = new Product { Name = "Disco", Description = "", Price = 100, Stock = 5, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await new AddCartItemUseCase(db).ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 2));

        var checkout = await new CheckoutCartUseCase(db, gateways: [new BankTransferPaymentGateway()]).ExecuteAsync(
            user.Id,
            new CheckoutCartRequest("HomeDelivery", "Calle 123", 0, 200, 0, "checkout-paid", PaymentProvider: "BankTransfer", PaymentIdempotencyKey: "payment-paid"));

        Assert.True(checkout.IsSuccess);
        Assert.Equal(5, product.Stock);
        Assert.Empty(db.CartItems);
        var payment = Assert.Single(db.Payments);
        var pendingOrder = Assert.Single(db.Orders);
        Assert.Equal(OrderStatus.Pending, pendingOrder.Status);
        Assert.False(pendingOrder.StockDeducted);
        Assert.Equal(pendingOrder.Id, payment.OrderId);
        Assert.Equal(pendingOrder.Id, checkout.Value!.OrderId);

        await new AddCartItemUseCase(db).ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 1));

        var approved = await new UpdatePaymentStatusUseCase(db).ExecuteAsync(
            payment.Id,
            new UpdatePaymentStatusRequest("Approved", null, "Transferencia acreditada"));

        Assert.True(approved.IsSuccess);
        Assert.Equal(3, product.Stock);
        Assert.Equal(1, (await db.CartItems.SingleAsync()).Quantity);
        var order = Assert.Single(db.Orders);
        Assert.Equal(pendingOrder.Id, order.Id);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.True(order.StockDeducted);
        Assert.Equal(order.Id, payment.OrderId);
        Assert.Equal(CheckoutStatus.Completed, db.CheckoutSessions.Single().Status);
        Assert.Single(db.StockMovements.Where(x => x.Type == StockMovementType.Sale));
    }

    [Fact]
    public async Task Approved_bank_transfer_without_stock_cancels_pending_order_without_negative_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = new Product { Name = "Última unidad", Description = "", Price = 100, Stock = 1, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await new AddCartItemUseCase(db).ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 1));
        var checkout = await new CheckoutCartUseCase(db, gateways: [new BankTransferPaymentGateway()]).ExecuteAsync(
            user.Id,
            new CheckoutCartRequest("HomeDelivery", "Calle 123", 0, 100, 0, "checkout-no-stock", PaymentProvider: "BankTransfer", PaymentIdempotencyKey: "payment-no-stock"));
        Assert.True(checkout.IsSuccess);

        product.Stock = 0;
        await db.SaveChangesAsync();
        var payment = await db.Payments.SingleAsync();
        var approved = await new UpdatePaymentStatusUseCase(db).ExecuteAsync(
            payment.Id,
            new UpdatePaymentStatusRequest("Approved", null, "Transferencia acreditada"));

        Assert.False(approved.IsSuccess);
        Assert.Equal("paid_checkout_stock_unavailable", approved.Error?.Code);
        Assert.Equal(0, product.Stock);
        var unavailableOrder = Assert.Single(db.Orders);
        Assert.Equal(OrderStatus.Canceled, unavailableOrder.Status);
        Assert.False(unavailableOrder.StockDeducted);
        Assert.Equal(PaymentStatus.Approved, payment.Status);
        Assert.Equal(CheckoutStatus.StockUnavailable, db.CheckoutSessions.Single().Status);
    }

    [Fact]
    public async Task Approved_bank_transfer_with_unavailable_coupon_cancels_pending_order()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = new Product { Name = "Banco", Description = "", Price = 100, Stock = 2, IsActive = true };
        var coupon = new Coupon
        {
            Code = "ULTIMO10", Name = "Último uso", Type = CouponType.FixedAmount,
            Value = 10, TotalUsageLimit = 1, IsActive = true
        };
        db.AddRange(product, coupon);
        await db.SaveChangesAsync();
        await new AddCartItemUseCase(db).ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 1));
        var cart = await db.Carts.SingleAsync(x => x.UserId == user.Id);
        cart.CouponId = coupon.Id;
        await db.SaveChangesAsync();

        var checkout = await new CheckoutCartUseCase(db, gateways: [new BankTransferPaymentGateway()]).ExecuteAsync(
            user.Id,
            new CheckoutCartRequest("HomeDelivery", "Calle 123", 0, 100, 10, "checkout-coupon", PaymentProvider: "BankTransfer", PaymentIdempotencyKey: "payment-coupon"));
        Assert.True(checkout.IsSuccess);

        coupon.IsActive = false;
        await db.SaveChangesAsync();
        var payment = await db.Payments.SingleAsync();
        var approved = await new UpdatePaymentStatusUseCase(db).ExecuteAsync(
            payment.Id,
            new UpdatePaymentStatusRequest("Approved", null, "Transferencia acreditada"));

        Assert.False(approved.IsSuccess);
        var unavailableOrder = Assert.Single(db.Orders);
        Assert.Equal(OrderStatus.Canceled, unavailableOrder.Status);
        Assert.False(unavailableOrder.StockDeducted);
        Assert.Equal(2, product.Stock);
        Assert.Equal(CheckoutStatus.StockUnavailable, db.CheckoutSessions.Single().Status);
    }

    [Fact]
    public async Task Payment_creation_failure_keeps_checkout_recoverable_after_cart_is_cleared()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = new Product { Name = "Mancuerna", Description = "", Price = 100, Stock = 2, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await new AddCartItemUseCase(db).ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 1));

        var result = await new CheckoutCartUseCase(db, gateways: [new FailingBankTransferGateway()]).ExecuteAsync(
            user.Id,
            new CheckoutCartRequest("HomeDelivery", "Calle 123", 0, 100, 0, "checkout-failed-payment", PaymentProvider: "BankTransfer", PaymentIdempotencyKey: "payment-failed"));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.CreationFailed.ToString(), result.Value!.Payment!.Status);
        Assert.Empty(db.CartItems);
        Assert.Equal(2, product.Stock);
        Assert.Empty(db.Orders);
    }

    [Fact]
    public async Task Mercado_pago_does_not_create_order_until_payment_is_approved()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = new Product { Name = "Barra", Description = "", Price = 150, Stock = 3, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await new AddCartItemUseCase(db).ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 1));

        var gateway = new PendingMercadoPagoGateway();
        var checkout = await new CheckoutCartUseCase(db, gateways: [gateway]).ExecuteAsync(
            user.Id,
            new CheckoutCartRequest("HomeDelivery", "Calle 123", 0, 150, 0, "checkout-mp", PaymentProvider: "MercadoPago", PaymentIdempotencyKey: "payment-mp"));

        Assert.True(checkout.IsSuccess);
        Assert.Null(checkout.Value!.OrderId);
        Assert.Empty(db.Orders);
        Assert.Equal(3, product.Stock);
        var payment = Assert.Single(db.Payments);
        Assert.Equal(PaymentStatus.Pending, payment.Status);

        gateway.ExternalReference = payment.ExternalReference;
        var approved = await new HandlePaymentWebhookUseCase(db, [gateway])
            .ExecuteAsync("MercadoPago", "mp-payment-1");

        Assert.True(approved.IsSuccess);
        var order = Assert.Single(db.Orders);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.True(order.StockDeducted);
        Assert.Equal(order.Id, payment.OrderId);
        Assert.Equal(2, product.Stock);
    }

    [Fact]
    public async Task Rejected_bank_transfer_cancels_pending_order_without_changing_stock()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = new Product { Name = "Kettlebell", Description = "", Price = 120, Stock = 4, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await new AddCartItemUseCase(db).ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 1));

        var checkout = await new CheckoutCartUseCase(db, gateways: [new BankTransferPaymentGateway()]).ExecuteAsync(
            user.Id,
            new CheckoutCartRequest("HomeDelivery", "Calle 123", 0, 120, 0, "checkout-rejected", PaymentProvider: "BankTransfer", PaymentIdempotencyKey: "payment-rejected"));
        Assert.True(checkout.IsSuccess);

        var payment = Assert.Single(db.Payments);
        var rejected = await new UpdatePaymentStatusUseCase(db).ExecuteAsync(
            payment.Id,
            new UpdatePaymentStatusRequest("Rejected", null, "Transferencia no acreditada"));

        Assert.True(rejected.IsSuccess);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal(OrderStatus.Canceled, Assert.Single(db.Orders).Status);
        Assert.Equal(CheckoutStatus.PaymentFailed, Assert.Single(db.CheckoutSessions).Status);
        Assert.Equal(4, product.Stock);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task Canceling_pending_bank_transfer_does_not_restore_unreserved_stock_and_closes_checkout()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var user = await SeedUserAsync(db);
        var product = new Product { Name = "Colchoneta", Description = "", Price = 90, Stock = 6, IsActive = true };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await new AddCartItemUseCase(db).ExecuteAsync(user.Id, new AddCartItemRequest(product.Id, 2));

        var checkout = await new CheckoutCartUseCase(db, gateways: [new BankTransferPaymentGateway()]).ExecuteAsync(
            user.Id,
            new CheckoutCartRequest("HomeDelivery", "Calle 123", 0, 180, 0, "checkout-canceled", PaymentProvider: "BankTransfer", PaymentIdempotencyKey: "payment-canceled"));
        Assert.True(checkout.IsSuccess);

        var order = Assert.Single(db.Orders);
        var canceled = await new CancelOrderUseCase(db).ExecuteAsync(
            order.Id, user.Id, canManageAll: true, request: new CancelOrderRequest("Transferencia vencida"));

        Assert.True(canceled.IsSuccess);
        Assert.Equal(OrderStatus.Canceled, order.Status);
        Assert.False(order.StockDeducted);
        Assert.Equal(PaymentStatus.Canceled, Assert.Single(db.Payments).Status);
        Assert.Equal(CheckoutStatus.PaymentFailed, Assert.Single(db.CheckoutSessions).Status);
        Assert.Equal(6, product.Stock);
        Assert.Empty(db.StockMovements);
    }

    private static async Task<User> SeedUserAsync(GymShop.Infrastructure.Data.GymShopDbContext db)
    {
        var user = new User { Name = "Cliente", Email = $"checkout-{Guid.NewGuid():N}@test.com", PasswordHash = "x", RoleId = 1, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class FailingBankTransferGateway : IPaymentGateway
    {
        public bool CanHandle(string provider) => string.Equals(provider, "BankTransfer", StringComparison.OrdinalIgnoreCase);

        public Task<PaymentPreferenceResult> CreatePreferenceAsync(Order order, string? idempotencyKey, string? externalReference = null, CancellationToken cancellationToken = default) =>
            throw new PaymentGatewayException("No se pudo iniciar la transferencia.");

        public Task<ProviderPaymentResult> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class PendingMercadoPagoGateway : IPaymentGateway
    {
        public string ExternalReference { get; set; } = string.Empty;

        public bool CanHandle(string provider) => string.Equals(provider, "MercadoPago", StringComparison.OrdinalIgnoreCase);

        public Task<PaymentPreferenceResult> CreatePreferenceAsync(Order order, string? idempotencyKey, string? externalReference = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaymentPreferenceResult("MercadoPago", "preference-1", "https://example.test/pay"));

        public Task<ProviderPaymentResult> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderPaymentResult(providerPaymentId, ExternalReference, "approved", 150, "ARS", null));
    }
}
