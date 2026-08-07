using GymShop.Application.Common;
using GymShop.Application.DTOs.Products;
using GymShop.Application.UseCases.Products;
using GymShop.Domain.Entities;
using GymShop.Tests.TestSupport;

namespace GymShop.Tests.UseCases;

public class ProductUseCaseTests
{
    [Fact]
    public async Task CreateProduct_persists_product()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var useCase = new CreateProductUseCase(db);

        var result = await useCase.ExecuteAsync(new CreateProductRequest(
            "Mancuerna",
            "Mancuerna 10kg",
            25000,
            5,
            "/images/mancuerna.jpeg"
        ));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Mancuerna", result.Value.Name);
        Assert.Single(db.Products);
    }

    [Fact]
    public async Task CreateProduct_rejects_invalid_price()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var useCase = new CreateProductUseCase(db);

        var result = await useCase.ExecuteAsync(new CreateProductRequest("Producto", null, 0, 1, null));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Validation, result.Error?.Type);
    }

    [Theory]
    [InlineData(151, 0, 0)]
    [InlineData(1, 1001, 0)]
    [InlineData(1, 0, 501)]
    public async Task CreateProduct_rejects_values_over_database_limits(int nameLength, int descriptionLength, int imageLength)
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var request = new CreateProductRequest(
            new string('N', nameLength),
            descriptionLength == 0 ? null : new string('D', descriptionLength),
            10,
            1,
            imageLength == 0 ? null : "/" + new string('i', imageLength - 1));

        var result = await new CreateProductUseCase(db).ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(AppErrorType.Validation, result.Error?.Type);
        Assert.Empty(db.Products);
    }

    [Fact]
    public async Task Public_catalog_and_lookup_hide_inactive_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var active = new Product { Name = "Activo", Price = 10, Stock = 1, IsActive = true };
        var inactive = new Product { Name = "Inactivo", Price = 10, Stock = 1, IsActive = false };
        db.Products.AddRange(active, inactive);
        await db.SaveChangesAsync();

        var catalog = await new GetProductsUseCase(db).ExecuteAsync(false, false);
        var forcedPublicCatalog = await new GetProductsUseCase(db).ExecuteAsync(true, false);
        var publicLookup = await new GetProductByIdUseCase(db).ExecuteAsync(inactive.Id, false);

        Assert.Collection(catalog, item => Assert.Equal(active.Id, item.Id));
        Assert.Collection(forcedPublicCatalog, item => Assert.Equal(active.Id, item.Id));
        Assert.Equal(AppErrorType.NotFound, publicLookup.Error?.Type);
    }

    [Fact]
    public async Task Admin_can_list_and_get_inactive_products()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var inactive = new Product { Name = "Inactivo", Price = 10, Stock = 1, IsActive = false };
        db.Products.Add(inactive);
        await db.SaveChangesAsync();

        var catalog = await new GetProductsUseCase(db).ExecuteAsync(true, true);
        var lookup = await new GetProductByIdUseCase(db).ExecuteAsync(inactive.Id, true);

        Assert.Single(catalog);
        Assert.True(lookup.IsSuccess);
        Assert.Equal(inactive.Id, lookup.Value!.Id);
    }

    [Fact]
    public async Task Stock_and_status_changes_create_safe_audit_entries()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var actor = new User { Name = "Admin", Email = "product-admin@test.com", PasswordHash = "secret-hash", RoleId = 2, IsActive = true };
        var product = new Product { Name = "Producto", Price = 10, Stock = 5, IsActive = true };
        db.AddRange(actor, product);
        await db.SaveChangesAsync();
        var auditContext = new FakeAuditContext(actor.Id, "corr-product");

        var stock = await new UpdateProductStockUseCase(db, auditContext).ExecuteAsync(product.Id, new UpdateProductStockRequest(8));
        var status = await new UpdateProductStatusUseCase(db, auditContext).ExecuteAsync(product.Id, new UpdateProductStatusRequest(false));

        Assert.True(stock.IsSuccess);
        Assert.True(status.IsSuccess);
        Assert.Equal(["ProductStockChanged", "ProductStatusChanged"], db.AuditEntries.Select(x => x.Action).ToArray());
        Assert.All(db.AuditEntries, entry => Assert.Equal("corr-product", entry.CorrelationId));
        Assert.DoesNotContain("secret-hash", string.Join('|', db.AuditEntries.Select(x => x.OldValue + x.NewValue + x.Reason)));
    }

    [Fact]
    public async Task Failed_product_change_does_not_create_success_audit()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var result = await new UpdateProductStockUseCase(db, new FakeAuditContext(1, "corr-failed"))
            .ExecuteAsync(999999, new UpdateProductStockRequest(4));

        Assert.False(result.IsSuccess);
        Assert.Empty(db.AuditEntries);
    }
}
