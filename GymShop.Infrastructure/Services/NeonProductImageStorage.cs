using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using GymShop.Application.Abstractions;
using GymShop.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymShop.Infrastructure.Services;

public sealed class NeonProductImageStorage : IProductImageStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly Uri _endpoint;
    private readonly ILogger<NeonProductImageStorage> _logger;

    public NeonProductImageStorage(IConfiguration configuration, IOptions<ProductImageStorageOptions> options, ILogger<NeonProductImageStorage> logger)
    {
        _logger = logger;
        _bucket = options.Value.BucketName;
        var endpoint = configuration["AWS_ENDPOINT_URL_S3"];
        var region = configuration["AWS_REGION"];
        var accessKey = configuration["AWS_ACCESS_KEY_ID"];
        var secretKey = configuration["AWS_SECRET_ACCESS_KEY"];
        if (string.IsNullOrWhiteSpace(_bucket) || string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(region)
            || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey) || !Uri.TryCreate(endpoint, UriKind.Absolute, out _endpoint!))
            throw new InvalidOperationException("La configuración de almacenamiento de imágenes está incompleta.");

        _s3 = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = _endpoint.ToString().TrimEnd('/'),
            AuthenticationRegion = region,
            ForcePathStyle = true
        });
    }

    public async Task<ProductImageUpload> UploadAsync(Stream content, string contentType, int? productId, CancellationToken cancellationToken = default)
    {
        var extension = contentType switch { "image/jpeg" => "jpg", "image/png" => "png", "image/webp" => "webp", _ => throw new ArgumentOutOfRangeException(nameof(contentType)) };
        var key = $"products/{(productId is > 0 ? productId.Value.ToString() : "draft")}/{Guid.NewGuid():N}.{extension}";
        try
        {
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket, Key = key, InputStream = content, ContentType = contentType,
                Headers = { CacheControl = "public,max-age=31536000,immutable" }
            }, cancellationToken);
            return new ProductImageUpload(BuildUrl(key), key);
        }
        catch (AmazonS3Exception exception)
        {
            _logger.LogError(exception, "Neon Object Storage rejected a product image upload.");
            throw new ProductImageStorageException("No se pudo almacenar la imagen.", exception);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!IsSafeKey(key)) return;
        try { await _s3.DeleteObjectAsync(_bucket, key, cancellationToken); }
        catch (AmazonS3Exception exception)
        {
            _logger.LogError(exception, "Neon Object Storage rejected deletion of product image {Key}.", key);
            throw new ProductImageStorageException("No se pudo eliminar la imagen.", exception);
        }
    }

    public bool TryGetManagedKey(string? url, out string key)
    {
        key = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var candidate)) return false;
        var prefix = $"/{Uri.EscapeDataString(_bucket)}/";
        if (!candidate.Scheme.Equals(_endpoint.Scheme, StringComparison.OrdinalIgnoreCase)
            || !candidate.Host.Equals(_endpoint.Host, StringComparison.OrdinalIgnoreCase)
            || !candidate.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) return false;
        key = Uri.UnescapeDataString(candidate.AbsolutePath[prefix.Length..]);
        return IsSafeKey(key);
    }

    private string BuildUrl(string key) => $"{_endpoint.ToString().TrimEnd('/')}/{Uri.EscapeDataString(_bucket)}/{string.Join('/', key.Split('/').Select(Uri.EscapeDataString))}";
    private static bool IsSafeKey(string key) => key.StartsWith("products/", StringComparison.Ordinal) && !key.Contains("..", StringComparison.Ordinal) && !key.StartsWith('/') && key.Length <= 1024;
}
