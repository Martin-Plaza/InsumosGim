using System.ComponentModel.DataAnnotations;
using GymShop.Application.Common;

namespace GymShop.Application.DTOs.Products;

public record ProductResponse(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    bool IsActive
);

public record CreateProductRequest(
    [Required, StringLength(ValidationLimits.ProductName)] string Name,
    [StringLength(ValidationLimits.ProductDescription)] string? Description,
    [SqlDecimal] decimal Price,
    [Range(0, int.MaxValue)] int Stock,
    [StringLength(ValidationLimits.ImageUrl), ProductImageUrl] string? ImageUrl
);

public record UpdateProductRequest(
    [Required, StringLength(ValidationLimits.ProductName)] string Name,
    [StringLength(ValidationLimits.ProductDescription)] string? Description,
    [SqlDecimal] decimal Price,
    [Range(0, int.MaxValue)] int Stock,
    [StringLength(ValidationLimits.ImageUrl), ProductImageUrl] string? ImageUrl,
    bool IsActive
);

public record UpdateProductStockRequest([Range(0, int.MaxValue)] int Stock);

public record UpdateProductStatusRequest(bool IsActive);

