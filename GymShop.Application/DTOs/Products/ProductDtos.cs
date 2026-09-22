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
    bool IsActive,
    CategorySummaryResponse? Category
);

public record CategorySummaryResponse(int Id, string Name, string Slug);

public record CategoryResponse(int Id, string Name, string Slug, string? Description, int DisplayOrder);

public record ProductQuery(
    string? Search = null,
    string? Category = null,
    bool? InStock = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool IncludeInactive = false);

public record CreateProductRequest(
    [Required, StringLength(ValidationLimits.ProductName)] string Name,
    [StringLength(ValidationLimits.ProductDescription)] string? Description,
    [SqlDecimal] decimal Price,
    [Range(0, int.MaxValue)] int Stock,
    [StringLength(ValidationLimits.ImageUrl), ProductImageUrl] string? ImageUrl,
    int? CategoryId = null
);

public record UpdateProductRequest(
    [Required, StringLength(ValidationLimits.ProductName)] string Name,
    [StringLength(ValidationLimits.ProductDescription)] string? Description,
    [SqlDecimal] decimal Price,
    [StringLength(ValidationLimits.ImageUrl), ProductImageUrl] string? ImageUrl,
    bool IsActive,
    int? CategoryId = null
);

public record UpdateProductStatusRequest(bool IsActive);

