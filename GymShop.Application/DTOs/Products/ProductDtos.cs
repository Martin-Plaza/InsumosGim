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
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl
);

public record UpdateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    bool IsActive
);

public record UpdateProductStockRequest(int Stock);

public record UpdateProductStatusRequest(bool IsActive);

