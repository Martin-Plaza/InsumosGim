using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using GymShop.Application.DTOs.Products;
using GymShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.UseCases.Products;

public interface IGetProductsUseCase
{
    Task<List<ProductResponse>> ExecuteAsync(bool includeInactive, bool canViewInactive, CancellationToken cancellationToken = default);
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

public interface IUpdateProductStockUseCase
{
    Task<AppResult> ExecuteAsync(int id, UpdateProductStockRequest request, CancellationToken cancellationToken = default);
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

    public async Task<List<ProductResponse>> ExecuteAsync(bool includeInactive, bool canViewInactive, CancellationToken cancellationToken = default)
    {
        var query = _db.Products.AsNoTracking();
        if (!includeInactive || !canViewInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderByDescending(x => x.Id)
            .Select(x => ProductMapper.ToResponse(x))
            .ToListAsync(cancellationToken);
    }
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
        var product = await _db.Products.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && (x.IsActive || canViewInactive), cancellationToken);
        return product is null
            ? AppResult<ProductResponse>.Failure(AppErrorType.NotFound, "Producto no encontrado.")
            : AppResult<ProductResponse>.Success(ProductMapper.ToResponse(product));
    }
}

public class CreateProductUseCase : ICreateProductUseCase
{
    private readonly IApplicationDbContext _db;

    public CreateProductUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<ProductResponse>> ExecuteAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ProductValidator.Validate(request.Name, request.Description, request.Price, request.Stock, request.ImageUrl);
        if (validationError is not null)
        {
            return AppResult<ProductResponse>.Failure(AppErrorType.Validation, validationError);
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Price = request.Price,
            Stock = request.Stock,
            ImageUrl = request.ImageUrl?.Trim(),
            IsActive = true
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        return AppResult<ProductResponse>.Success(ProductMapper.ToResponse(product));
    }
}

public class UpdateProductUseCase : IUpdateProductUseCase
{
    private readonly IApplicationDbContext _db;

    public UpdateProductUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult<ProductResponse>> ExecuteAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return AppResult<ProductResponse>.Failure(AppErrorType.NotFound, "Producto no encontrado.");
        }

        var validationError = ProductValidator.Validate(request.Name, request.Description, request.Price, request.Stock, request.ImageUrl);
        if (validationError is not null)
        {
            return AppResult<ProductResponse>.Failure(AppErrorType.Validation, validationError);
        }

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Price = request.Price;
        product.Stock = request.Stock;
        product.ImageUrl = request.ImageUrl?.Trim();
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

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

public class UpdateProductStockUseCase : IUpdateProductStockUseCase
{
    private readonly IApplicationDbContext _db;

    public UpdateProductStockUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult> ExecuteAsync(int id, UpdateProductStockRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Stock < 0)
        {
            return AppResult.Failure(AppErrorType.Validation, "El stock no puede ser negativo.");
        }

        var product = await _db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return AppResult.Failure(AppErrorType.NotFound, "Producto no encontrado.");
        }

        product.Stock = request.Stock;
        product.UpdatedAt = DateTime.UtcNow;
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

public class UpdateProductStatusUseCase : IUpdateProductStatusUseCase
{
    private readonly IApplicationDbContext _db;

    public UpdateProductStatusUseCase(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppResult> ExecuteAsync(int id, UpdateProductStatusRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return AppResult.Failure(AppErrorType.NotFound, "Producto no encontrado.");
        }

        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
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

        if (stock < 0)
        {
            return "El stock no puede ser negativo.";
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
            product.IsActive
        );
    }
}


