using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Products;
using GymShop.Application.UseCases.Stock;
using GymShop.Domain.Entities;
using GymShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Products;

public interface IGetProductsUseCase
{
    Task<List<ProductResponse>> ExecuteAsync(ProductQuery query, bool canViewInactive, CancellationToken cancellationToken = default);
}

public interface IGetCategoriesUseCase
{
    Task<List<CategoryResponse>> ExecuteAsync(CancellationToken cancellationToken = default);
}

public interface IGetProductByIdUseCase
{
    Task<AppResult<ProductResponse>> ExecuteAsync(int id, bool canViewInactive, CancellationToken cancellationToken = default);
}

public interface ICreateProductUseCase
{
    Task<AppResult<ProductResponse>> ExecuteAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
}

public interface IUpdateProductUseCase
{
    Task<AppResult<ProductResponse>> ExecuteAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default);
}

public interface IUpdateProductStatusUseCase
{
    Task<AppResult> ExecuteAsync(int id, UpdateProductStatusRequest request, CancellationToken cancellationToken = default);
}

public class GetProductsUseCase : IGetProductsUseCase
{
    private readonly IApplicationDbContext _db;

    public GetProductsUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductResponse>> ExecuteAsync(ProductQuery request, bool canViewInactive, CancellationToken cancellationToken = default)
    {
        IQueryable<Product> query = _db.Products.AsNoTracking().Include(x => x.Category);
        if (!request.IncludeInactive || !canViewInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(search) ||
                                     (x.Description != null && x.Description.ToLower().Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim().ToLower();
            query = query.Where(x => x.Category != null && x.Category.Slug == category);
        }
        if (request.InStock.HasValue)
            query = request.InStock.Value ? query.Where(x => x.Stock > 0) : query.Where(x => x.Stock == 0);
        if (request.MinPrice.HasValue) query = query.Where(x => x.Price >= request.MinPrice.Value);
        if (request.MaxPrice.HasValue) query = query.Where(x => x.Price <= request.MaxPrice.Value);

        return await query
            .OrderByDescending(x => x.Id)
            .Select(x => ProductMapper.ToResponse(x))
            .ToListAsync(cancellationToken);
    }
}

public class GetCategoriesUseCase : IGetCategoriesUseCase
{
    private readonly IApplicationDbContext _db;
    public GetCategoriesUseCase(IApplicationDbContext db) => _db = db;

    public Task<List<CategoryResponse>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        _db.Categories.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new CategoryResponse(x.Id, x.Name, x.Slug, x.Description, x.DisplayOrder, x.Color))
            .ToListAsync(cancellationToken);
}

public class GetProductByIdUseCase : IGetProductByIdUseCase
{
    private readonly IApplicationDbContext _db;

    public GetProductByIdUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<ProductResponse>> ExecuteAsync(int id, bool canViewInactive, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.AsNoTracking().Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == id && (x.IsActive || canViewInactive), cancellationToken);
        return product is null
            ? AppResult<ProductResponse>.Failure(AppErrorType.NotFound, "Producto no encontrado.")
            : AppResult<ProductResponse>.Success(ProductMapper.ToResponse(product));
    }
}

public class CreateProductUseCase : ICreateProductUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditContext _auditContext;

    public CreateProductUseCase(IApplicationDbContext db, IAuditContext? auditContext = null)
    {
        _db = db;
        _auditContext = auditContext ?? SystemAuditContext.Instance;
    }

    public async Task<AppResult<ProductResponse>> ExecuteAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ProductValidator.Validate(request.Name, request.Description, request.Price, request.Stock, request.ImageUrl);
        if (validationError is not null)
        {
            return AppResult<ProductResponse>.Failure(AppErrorType.Validation, validationError);
        }
        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            return AppResult<ProductResponse>.Failure(AppErrorType.Validation, "Agregá una imagen o su URL.");

        var category = request.CategoryId.HasValue
            ? await _db.Categories.SingleOrDefaultAsync(x => x.Id == request.CategoryId && x.IsActive, cancellationToken)
            : null;
        if (request.CategoryId.HasValue && category is null)
            return AppResult<ProductResponse>.Failure(AppErrorType.Validation, "La categoría indicada no existe o está inactiva.");

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            Stock = request.Stock,
            ImageUrl = request.ImageUrl?.Trim(),
            IsActive = true,
            Category = category
        };

        _db.Products.Add(product);
        if (product.Stock > 0)
        {
            StockMovementRecorder.Add(_db, product, StockMovementType.InitialStock, product.Stock, 0,
                "Stock inicial del producto", _auditContext.ActorUserId);
        }
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult<ProductResponse>.Success(ProductMapper.ToResponse(product));
    }
}

public class UpdateProductUseCase : IUpdateProductUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditContext? _auditContext;

    public UpdateProductUseCase(IApplicationDbContext db, IAuditContext? auditContext = null)
    {
        _db = db;
        _auditContext = auditContext;
    }

    public async Task<AppResult<ProductResponse>> ExecuteAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return AppResult<ProductResponse>.Failure(AppErrorType.NotFound, "Producto no encontrado.");
        }

        var validationError = ProductValidator.ValidateGeneral(request.Name, request.Description, request.Price, request.ImageUrl);
        if (validationError is not null)
        {
            return AppResult<ProductResponse>.Failure(AppErrorType.Validation, validationError);
        }

        var category = request.CategoryId.HasValue
            ? await _db.Categories.SingleOrDefaultAsync(x => x.Id == request.CategoryId && (x.IsActive || x.Id == product.CategoryId), cancellationToken)
            : null;
        if (request.CategoryId.HasValue && category is null)
            return AppResult<ProductResponse>.Failure(AppErrorType.Validation, "La categoría indicada no existe o está inactiva.");

        var oldValue = new { product.Name, product.Price, product.Stock, product.IsActive };
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.ImageUrl = request.ImageUrl?.Trim();
        product.IsActive = request.IsActive;
        product.Category = category;
        product.UpdatedAt = DateTime.UtcNow;
        AuditTrail.Add(_db, _auditContext, "ProductUpdated", "Product", product.Id, oldValue,
            new { product.Name, product.Price, product.Stock, product.IsActive });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AppResult<ProductResponse>.Failure(AppErrorType.Conflict, "El producto fue modificado por otra operacion. Volve a intentar.");
        }

        return AppResult<ProductResponse>.Success(ProductMapper.ToResponse(product));
    }
}

public class UpdateProductStatusUseCase : IUpdateProductStatusUseCase
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditContext? _auditContext;

    public UpdateProductStatusUseCase(IApplicationDbContext db, IAuditContext? auditContext = null)
    {
        _db = db;
        _auditContext = auditContext;
    }

    public async Task<AppResult> ExecuteAsync(int id, UpdateProductStatusRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return AppResult.Failure(AppErrorType.NotFound, "Producto no encontrado.");
        }

        if (product.IsActive == request.IsActive) return AppResult.Success();

        var oldStatus = product.IsActive;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        AuditTrail.Add(_db, _auditContext, "ProductStatusChanged", "Product", product.Id,
            new { isActive = oldStatus }, new { isActive = product.IsActive });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AppResult.Failure(AppErrorType.Conflict, "El producto fue modificado por otra operacion. Volve a intentar.");
        }

        return AppResult.Success();
    }
}

public static class ProductValidator
{
    public static string? Validate(string name, string? description, decimal price, int stock, string? imageUrl)
    {
        var generalError = ValidateGeneral(name, description, price, imageUrl);
        if (generalError is not null) return generalError;
        return stock < 0 ? "El stock no puede ser negativo." : null;
    }

    public static string? ValidateGeneral(string name, string? description, decimal price, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "El nombre es obligatorio.";
        }

        if (name.Trim().Length > ValidationLimits.ProductName) return "El nombre no puede superar los 150 caracteres.";
        if (description?.Trim().Length > ValidationLimits.ProductDescription) return "La descripcion no puede superar los 1000 caracteres.";

        if (price <= 0)
        {
            return "El precio debe ser mayor a cero.";
        }

        if (price > 9999999999999999.99m || decimal.Round(price, 2) != price)
        {
            return "El precio debe ser compatible con decimal(18,2).";
        }

        if (imageUrl?.Trim().Length > ValidationLimits.ImageUrl) return "ImageUrl no puede superar los 500 caracteres.";
        if (!new ProductImageUrlAttribute().IsValid(imageUrl?.Trim()))
        {
            return "ImageUrl debe ser una URL http/https o una ruta web local valida.";
        }

        return null;
    }
}

internal static class ProductMapper
{
    public static ProductResponse ToResponse(Product product)
    {
        return new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Stock,
            product.ImageUrl,
            product.IsActive,
            product.Category is null ? null : new CategorySummaryResponse(product.Category.Id, product.Category.Name, product.Category.Slug)
        );
    }
}


