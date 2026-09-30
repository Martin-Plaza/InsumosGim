using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GymShop.Application.Abstractions;
using GymShop.Application.DTOs.Payments;
using GymShop.Application.UseCases.Payments;
using GymShop.Infrastructure.Configuration;
using GymShop.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace GymShop.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public class PaymentsController : ApiControllerBase
{
    private readonly ICreatePaymentUseCase _createPayment;
    private readonly IGetPaymentByIdUseCase _getPaymentById;
    private readonly IGetOrderPaymentsUseCase _getOrderPayments;
    private readonly IUpdatePaymentStatusUseCase _updatePaymentStatus;
    private readonly IHandlePaymentWebhookUseCase _handlePaymentWebhook;
    private readonly ICurrentUserService _currentUser;
    private readonly MercadoPagoOptions _mercadoPagoOptions;
    private readonly IGymShopRequestLimiter _requestLimiter;
    private readonly BankTransferOptions _bankTransferOptions;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ICreatePaymentUseCase createPayment,
        IGetPaymentByIdUseCase getPaymentById,
        IGetOrderPaymentsUseCase getOrderPayments,
        IUpdatePaymentStatusUseCase updatePaymentStatus,
        IHandlePaymentWebhookUseCase handlePaymentWebhook,
        ICurrentUserService currentUser,
        IOptions<MercadoPagoOptions> mercadoPagoOptions,
        IGymShopRequestLimiter requestLimiter,
        IOptions<BankTransferOptions> bankTransferOptions,
        ILogger<PaymentsController> logger)
    {
        _createPayment = createPayment;
        _getPaymentById = getPaymentById;
        _getOrderPayments = getOrderPayments;
        _updatePaymentStatus = updatePaymentStatus;
        _handlePaymentWebhook = handlePaymentWebhook;
        _currentUser = currentUser;
        _mercadoPagoOptions = mercadoPagoOptions.Value;
        _requestLimiter = requestLimiter;
        _bankTransferOptions = bankTransferOptions.Value;
        _logger = logger;
    }

    [HttpGet("bank-transfer-details")]
    public ActionResult<BankTransferDetailsResponse> GetBankTransferDetails() => Ok(new BankTransferDetailsResponse(
        _bankTransferOptions.BankName,
        _bankTransferOptions.AccountHolder,
        _bankTransferOptions.Cbu,
        _bankTransferOptions.Alias,
        _bankTransferOptions.Cuit));

    [HttpGet("methods")]
    public ActionResult<PaymentMethodsResponse> GetMethods()
    {
        var mercadoPagoAvailable = IsMercadoPagoAvailable(_mercadoPagoOptions);
        return Ok(new PaymentMethodsResponse(
            BankTransferAvailable: true,
            MercadoPagoAvailable: mercadoPagoAvailable,
            MercadoPagoUnavailableReason: mercadoPagoAvailable ? null : "Mercado Pago no está disponible en este momento."));
    }

    public static bool IsMercadoPagoAvailable(MercadoPagoOptions options) =>
        options.Enabled && !string.IsNullOrWhiteSpace(options.AccessToken);

    [HttpPost("/api/orders/{orderId:int}/payments")]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PaymentResponse>> CreateForOrder(int orderId, CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var userDecision = _requestLimiter.Acquire(RateLimitPolicies.PaymentUser, userId.ToString());
        if (!userDecision.IsAllowed) return RateLimitResponse.Create(HttpContext, userDecision);

        var decision = _requestLimiter.Acquire(RateLimitPolicies.PaymentOrder, orderId.ToString());
        if (!decision.IsAllowed) return RateLimitResponse.Create(HttpContext, decision);

        var canManageAll = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var result = await _createPayment.ExecuteAsync(orderId, userId, canManageAll, request, cancellationToken);
        if (result.IsSuccess && string.Equals(result.Value!.Status, "Creating", StringComparison.Ordinal))
        {
            return AcceptedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
        }

        return FromResult(result);
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
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> UpdateStatus(int id, UpdatePaymentStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await _updatePaymentStatus.ExecuteAsync(id, request, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("mercadopago/webhook")]
    [EnableRateLimiting(RateLimitPolicies.WebhookIp)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> MercadoPagoWebhook(
        [FromQuery(Name = "data.id")] string? queryDataId,
        [FromQuery(Name = "id")] string? legacyId,
        [FromQuery(Name = "topic")] string? legacyTopic,
        CancellationToken cancellationToken)
    {
        if (!_mercadoPagoOptions.Enabled)
        {
            return NotFound(new { message = "La integracion de Mercado Pago no esta habilitada." });
        }

        var notification = await GetMercadoPagoNotificationAsync(
            queryDataId, legacyId, legacyTopic, Request, cancellationToken);
        if (notification is null)
        {
            _logger.LogWarning(
                "Unrecognized Mercado Pago notification format. HasLegacyId={HasLegacyId}, HasTopic={HasTopic}, HasJsonContentType={HasJsonContentType}, ContentLength={ContentLength}",
                Request.Query.ContainsKey("id"), Request.Query.ContainsKey("topic"),
                Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true,
                Request.ContentLength);
            return BadRequest(new { message = "No se encontro data.id en la notificacion." });
        }

        var secret = _mercadoPagoOptions.WebhookSecret;
        if (notification.RequiresSignature &&
            !string.IsNullOrWhiteSpace(secret) &&
            !MercadoPagoWebhookSignatureValidator.IsValid(
                Request.Headers["x-signature"], Request.Headers["x-request-id"], notification.PaymentId, secret))
        {
            return Unauthorized(new { message = "Firma de Mercado Pago invalida." });
        }

        var decision = _requestLimiter.Acquire(RateLimitPolicies.WebhookGlobal, "all");
        if (!decision.IsAllowed) return RateLimitResponse.Create(HttpContext, decision);

        var result = await _handlePaymentWebhook.ExecuteAsync("MercadoPago", notification.PaymentId, cancellationToken);
        return result.IsSuccess ? Ok(new { received = true }) : ToErrorResponse(result.Error!);
    }

    private static async Task<MercadoPagoNotification?> GetMercadoPagoNotificationAsync(
        string? queryDataId,
        string? legacyId,
        string? legacyTopic,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(queryDataId))
        {
            return new MercadoPagoNotification(queryDataId, RequiresSignature: true);
        }

        if (request.ContentLength != 0)
        {
            try
            {
                using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
                var body = document.RootElement;
                if (body.ValueKind == JsonValueKind.Object &&
                    body.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty("id", out var id))
                {
                    var bodyDataId = id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
                    if (!string.IsNullOrWhiteSpace(bodyDataId))
                    {
                        return new MercadoPagoNotification(bodyDataId, RequiresSignature: true);
                    }
                }
            }
            catch (JsonException)
            {
                // A malformed or non-JSON notification cannot supply a signed Webhook payment ID.
            }
        }

        if (string.Equals(legacyTopic, "payment", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(legacyId) &&
            legacyId.All(char.IsAsciiDigit))
        {
            // Legacy IPN does not carry the Webhook HMAC headers. Authenticity is established
            // server-to-server by loading this payment with our Mercado Pago access token and
            // validating its external reference, amount and currency before applying any state.
            return new MercadoPagoNotification(legacyId, RequiresSignature: false);
        }

        return null;
    }

    private sealed record MercadoPagoNotification(string PaymentId, bool RequiresSignature);
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
