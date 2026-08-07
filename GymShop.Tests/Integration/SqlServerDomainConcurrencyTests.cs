using GymShop.Application.DTOs.Carts;
using GymShop.Application.DTOs.Products;
using GymShop.Application.UseCases.Carts;
using GymShop.Application.UseCases.Products;
using GymShop.Domain.Entities;
using GymShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GymShop.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Category", "SqlServer")]
[Trait("Category", "Concurrency")]
public sealed class SqlServerDomainConcurrencyTests
{
    [Fact]
    public async Task Concurrent_checkouts_cannot_sell_the_same_last_stock_twice()
    {
        await using var database = await SqlTestDatabase.CreateMigratedAsync();
        var (firstUser, secondUser, productId) = await SeedTwoCartsAsync(database, stock: 1);
        var barrier = new ProductSaveBarrier(2);
        await using var firstDb = database.CreateContext(barrier);
        await using var secondDb = database.CreateContext(barrier);

        var firstTask = Capture(() => new CheckoutCartUseCase(firstDb, new EfTransactionManager(firstDb))
            .ExecuteAsync(firstUser, new CheckoutCartRequest("First Address")));
        var secondTask = Capture(() => new CheckoutCartUseCase(secondDb, new EfTransactionManager(secondDb))
            .ExecuteAsync(secondUser, new CheckoutCartRequest("Second Address")));
        await barrier.AllArrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        barrier.Release.TrySetResult();
        var outcomes = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(1, outcomes.Count(x => x.Success));
        await using var verification = database.CreateContext();
        Assert.Equal(0, (await verification.Products.SingleAsync(x => x.Id == productId)).Stock);
        Assert.Single(await verification.Orders.ToListAsync());
        Assert.Single(await verification.CartItems.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_stock_updates_have_one_winner_and_one_conflict()
    {
        await using var database = await SqlTestDatabase.CreateMigratedAsync();
        var seed = await database.SeedPendingOrderAsync();
        await using var lookup = database.CreateContext();
        var productId = await lookup.OrderItems.Where(x => x.OrderId == seed.OrderId).Select(x => x.ProductId).SingleAsync();
        var barrier = new ProductSaveBarrier(2);
        await using var firstDb = database.CreateContext(barrier);
        await using var secondDb = database.CreateContext(barrier);
        var first = new UpdateProductStockUseCase(firstDb).ExecuteAsync(productId, new UpdateProductStockRequest(10));
        var second = new UpdateProductStockUseCase(secondDb).ExecuteAsync(productId, new UpdateProductStockRequest(20));
        await barrier.AllArrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        barrier.Release.TrySetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, x => x.IsSuccess);
        Assert.Single(results, x => !x.IsSuccess && x.Error?.Type == GymShop.Application.Common.AppErrorType.Conflict);
        await using var verification = database.CreateContext();
        Assert.Contains((await verification.Products.SingleAsync(x => x.Id == productId)).Stock, new[] { 10, 20 });
    }

    private static async Task<(int FirstUser, int SecondUser, int ProductId)> SeedTwoCartsAsync(SqlTestDatabase database, int stock)
    {
        await using var db = database.CreateContext();
        var role = await db.Roles.SingleAsync(x => x.Name == "User");
        var first = new User { Email = $"first-{Guid.NewGuid():N}@test.com", Name = "First", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        var second = new User { Email = $"second-{Guid.NewGuid():N}@test.com", Name = "Second", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        var product = new Product { Name = "Last Stock", Price = 100, Stock = stock, IsActive = true };
        db.AddRange(first, second, product);
        await db.SaveChangesAsync();
        var firstCart = new Cart { UserId = first.Id };
        firstCart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1 });
        var secondCart = new Cart { UserId = second.Id };
        secondCart.Items.Add(new CartItem { ProductId = product.Id, Quantity = 1 });
        db.Carts.AddRange(firstCart, secondCart);
        await db.SaveChangesAsync();
        return (first.Id, second.Id, product.Id);
    }

    private static async Task<(bool Success, Exception? Error)> Capture<T>(Func<Task<T>> operation)
    {
        try { await operation(); return (true, null); }
        catch (Exception ex) { return (false, ex); }
    }

    private sealed class ProductSaveBarrier(int arrivals) : SaveChangesInterceptor
    {
        private int _arrivals;
        public TaskCompletionSource AllArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var changesProduct = eventData.Context?.ChangeTracker.Entries<Product>()
                .Any(x => x.State == EntityState.Modified) == true;
            if (changesProduct)
            {
                if (Interlocked.Increment(ref _arrivals) == arrivals) AllArrived.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
