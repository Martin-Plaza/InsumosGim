using GymShop.Application.Abstractions;
using GymShop.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Route("api/products/images")]
[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class ProductImagesController : ControllerBase
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    private readonly IProductImageStorage _storage;
    private readonly IApplicationDbContext _db;
    private readonly IAuditContext _auditContext;

    public ProductImagesController(IProductImageStorage storage, IApplicationDbContext db, IAuditContext auditContext)
    {
        _storage = storage; _db = db; _auditContext = auditContext;
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
    [ProducesResponseType(typeof(ProductImageUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ProductImageUploadResponse>> Upload([FromForm] IFormFile? file, [FromForm] int? productId, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Seleccioná una imagen." });
        if (file.Length > MaxFileSize) return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "La imagen no puede superar los 5 MB." });

        await using var input = file.OpenReadStream();
        await using var buffer = new MemoryStream((int)file.Length);
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var contentType = DetectContentType(bytes);
        if (contentType is null) return BadRequest(new { message = "Solo se permiten imágenes JPEG, PNG o WebP válidas." });

        ProductImageUpload? uploaded = null;
        try
        {
            buffer.Position = 0;
            uploaded = await _storage.UploadAsync(buffer, contentType, productId, cancellationToken);
            AuditTrail.Add(_db, _auditContext, "ProductImageUploaded", "ProductImage", uploaded.Key, null,
                new { uploaded.Key, contentType, size = file.Length, productId });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (ProductImageStorageException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "El servicio de imágenes no está disponible. Intentá nuevamente." });
        }
        catch
        {
            if (uploaded is not null)
            {
                try { await _storage.DeleteAsync(uploaded.Key, CancellationToken.None); } catch (ProductImageStorageException) { }
            }
            throw;
        }

        return Created(uploaded.Url, new ProductImageUploadResponse(uploaded.Url, uploaded.Key));
    }

    [HttpDelete]
    public async Task<ActionResult> Delete(ProductImageDeleteRequest request, CancellationToken cancellationToken)
    {
        var key = request.Key?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(key) && !_storage.TryGetManagedKey(request.Url, out key)) return NoContent();
        try { await _storage.DeleteAsync(key, cancellationToken); }
        catch (ProductImageStorageException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "El servicio de imágenes no está disponible. Intentá nuevamente." });
        }
        return NoContent();
    }

    public static string? DetectContentType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })) return "image/png";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}

public sealed record ProductImageUploadResponse(string Url, string Key);
public sealed record ProductImageDeleteRequest(string? Key, string? Url);
