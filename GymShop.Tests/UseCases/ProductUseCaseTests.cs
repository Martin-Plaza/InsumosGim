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
}
