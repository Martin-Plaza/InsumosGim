using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Payments;
using GymShop.Application.UseCases.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public class PaymentsController : ApiControllerBase
{
    private readonly ICreateCurrentPaymentUseCase _createCurrentPayment;
    private readonly IGetPaymentByIdUseCase _getPaymentById;
    private readonly IGetOrderPaymentsUseCase _getOrderPayments;
    private readonly IUpdatePaymentStatusUseCase _updatePaymentStatus;
    private readonly IHandlePaymentWebhookUseCase _handlePaymentWebhook;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration _configuration;

    public PaymentsController(
        ICreateCurrentPaymentUseCase createCurrentPayment,
        IGetPaymentByIdUseCase getPaymentById,
        IGetOrderPaymentsUseCase getOrderPayments,
        IUpdatePaymentStatusUseCase updatePaymentStatus,
        IHandlePaymentWebhookUseCase handlePaymentWebhook,
        ICurrentUserService currentUser,
        IConfiguration configuration)
    {
        _createCurrentPayment = createCurrentPayment;
        _getPaymentById = getPaymentById;
        _getOrderPayments = getOrderPayments;
        _updatePaymentStatus = updatePaymentStatus;
        _handlePaymentWebhook = handlePaymentWebhook;
        _currentUser = currentUser;
        _configuration = configuration;
    }

    [HttpPost("current")]
    public async Task<ActionResult<PaymentResponse>> CreateForCurrentOrder(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _createCurrentPayment.ExecuteAsync(_currentUser.UserId, request, cancellationToken));
    }


    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var canManageAll = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return FromResult(await _getPaymentById.ExecuteAsync(id, _currentUser.UserId, canManageAll, cancellationToken));
    }

    [HttpGet("orders/{orderId:int}")]
    public async Task<ActionResult<List<PaymentResponse>>> GetByOrder(int orderId, CancellationToken cancellationToken)
    {
        var canManageAll = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return FromResult(await _getOrderPayments.ExecuteAsync(orderId, _currentUser.UserId, canManageAll, cancellationToken));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost("{id:int}/status")]
    public async Task<ActionResult<PaymentResponse>> UpdateStatus(int id, UpdatePaymentStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updatePaymentStatus.ExecuteAsync(id, request, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("mercadopago/webhook")]
    public async Task<ActionResult> MercadoPagoWebhook([FromQuery(Name = "data.id")] string? queryDataId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var dataId = GetMercadoPagoPaymentId(queryDataId, body);
        if (string.IsNullOrWhiteSpace(dataId))
        {
            return BadRequest(new { message = "No se encontro data.id en la notificacion." });
        }

        var secret = _configuration["MercadoPago:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(secret) && !MercadoPagoWebhookSignatureValidator.IsValid(Request.Headers["x-signature"], Request.Headers["x-request-id"], dataId, secret))
        {
            return Unauthorized(new { message = "Firma de Mercado Pago invalida." });
        }

        var result = await _handlePaymentWebhook.ExecuteAsync("MercadoPago", dataId, cancellationToken);
        return result.IsSuccess ? Ok(new { received = true }) : ToErrorResponse(result.Error!);
    }

    private static string? GetMercadoPagoPaymentId(string? queryDataId, JsonElement body)
    {
        if (!string.IsNullOrWhiteSpace(queryDataId))
        {
            return queryDataId;
        }

        if (body.ValueKind == JsonValueKind.Object &&
            body.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("id", out var id))
        {
            return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
        }

        return null;
    }
}

public static class MercadoPagoWebhookSignatureValidator
{
    public static bool IsValid(string? xSignature, string? xRequestId, string dataId, string secret)
    {
        if (string.IsNullOrWhiteSpace(xSignature) || string.IsNullOrWhiteSpace(xRequestId))
        {
            return false;
        }

        var parts = xSignature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(part => part.Length == 2)
            .ToDictionary(part => part[0], part => part[1], StringComparer.OrdinalIgnoreCase);

        if (!parts.TryGetValue("ts", out var timestamp) || !parts.TryGetValue("v1", out var receivedSignature))
        {
            return false;
        }

        var manifest = $"id:{dataId};request-id:{xRequestId};ts:{timestamp};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
        var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(receivedSignature.ToLowerInvariant()));
    }
}
