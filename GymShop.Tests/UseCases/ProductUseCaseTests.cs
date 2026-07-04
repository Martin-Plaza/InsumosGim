using GymShop.Application.Common;
using GymShop.Application.DTOs.Products;
using GymShop.Application.UseCases.Products;
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
}
