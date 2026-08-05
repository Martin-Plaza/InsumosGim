using GymShop.Application.DTOs.Products;
using GymShop.Application.UseCases.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ApiControllerBase
{
    private readonly IGetProductsUseCase _getProducts;
    private readonly IGetProductByIdUseCase _getProductById;
    private readonly ICreateProductUseCase _createProduct;
    private readonly IUpdateProductUseCase _updateProduct;
    private readonly IUpdateProductStockUseCase _updateProductStock;
    private readonly IUpdateProductStatusUseCase _updateProductStatus;

    public ProductsController(
        IGetProductsUseCase getProducts,
        IGetProductByIdUseCase getProductById,
        ICreateProductUseCase createProduct,
        IUpdateProductUseCase updateProduct,
        IUpdateProductStockUseCase updateProductStock,
        IUpdateProductStatusUseCase updateProductStatus)
    {
        _getProducts = getProducts;
        _getProductById = getProductById;
        _createProduct = createProduct;
        _updateProduct = updateProduct;
        _updateProductStock = updateProductStock;
        _updateProductStatus = updateProductStatus;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ProductResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var canViewInactive = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        if (includeInactive && !canViewInactive)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
        }

        return Ok(await _getProducts.ExecuteAsync(includeInactive, canViewInactive, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var canViewInactive = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return FromResult(await _getProductById.ExecuteAsync(id, canViewInactive, cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _createProduct.ExecuteAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : ToErrorResponse(result.Error!);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Update(int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updateProduct.ExecuteAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPatch("{id:int}/stock")]
    public async Task<ActionResult> UpdateStock(int id, UpdateProductStockRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updateProductStock.ExecuteAsync(id, request, cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult> UpdateStatus(int id, UpdateProductStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updateProductStatus.ExecuteAsync(id, request, cancellationToken));
    }
}
