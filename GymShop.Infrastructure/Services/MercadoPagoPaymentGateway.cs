using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GymShop.Application.Abstractions;
using GymShop.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace GymShop.Infrastructure.Services;

public class MercadoPagoPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MercadoPagoPaymentGateway(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public bool CanHandle(string provider) => string.Equals(provider, "MercadoPago", StringComparison.OrdinalIgnoreCase);

    public async Task<PaymentPreferenceResult> CreatePreferenceAsync(Order order, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        ConfigureAuthorization();

        var notificationUrl = _configuration["MercadoPago:NotificationUrl"];
        var successUrl = FormatUrl(_configuration["MercadoPago:SuccessUrl"], order.Id);
        var failureUrl = FormatUrl(_configuration["MercadoPago:FailureUrl"], order.Id);
        var pendingUrl = FormatUrl(_configuration["MercadoPago:PendingUrl"], order.Id);

        var payload = new Dictionary<string, object?>
        {
            ["items"] = order.Items.Select(item => new
            {
                id = item.ProductId.ToString(),
                title = item.ProductName,
                quantity = item.Quantity,
                currency_id = "ARS",
                unit_price = item.UnitPrice
            }).ToList(),
            ["payer"] = new
            {
                email = order.User.Email
            },
            ["external_reference"] = $"order-{order.Id}"
        };

        if (IsPublicCallbackUrl(notificationUrl))
        {
            payload["notification_url"] = notificationUrl;
        }

        var backUrls = new Dictionary<string, string>();
        if (IsPublicCallbackUrl(successUrl))
        {
            backUrls["success"] = successUrl!;
        }

        if (IsPublicCallbackUrl(failureUrl))
        {
            backUrls["failure"] = failureUrl!;
        }

        if (IsPublicCallbackUrl(pendingUrl))
        {
            backUrls["pending"] = pendingUrl!;
        }

        if (backUrls.Count > 0)
        {
            payload["back_urls"] = backUrls;
        }


        using var request = new HttpRequestMessage(HttpMethod.Post, "checkout/preferences")
        {
            Content = JsonContent.Create(payload)
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException($"Mercado Pago rechazo la preferencia: {(int)response.StatusCode} {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var preferenceId = root.GetProperty("id").GetString();
        var useSandbox = _configuration.GetValue<bool>("MercadoPago:UseSandboxInitPoint");
        var checkoutUrl = ReadString(root, useSandbox ? "sandbox_init_point" : "init_point") ?? ReadString(root, "init_point");

        if (string.IsNullOrWhiteSpace(preferenceId) || string.IsNullOrWhiteSpace(checkoutUrl))
        {
            throw new PaymentGatewayException("Mercado Pago no devolvio preference id o checkout url.");
        }

        return new PaymentPreferenceResult("MercadoPago", preferenceId, checkoutUrl);
    }

    public async Task<ProviderPaymentResult> GetPaymentAsync(string providerPaymentId, CancellationToken cancellationToken = default)
    {
        ConfigureAuthorization();

        using var response = await _httpClient.GetAsync($"v1/payments/{providerPaymentId}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException($"Mercado Pago no devolvio el pago: {(int)response.StatusCode} {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return new ProviderPaymentResult(
            ReadString(root, "id") ?? providerPaymentId,
            ReadString(root, "external_reference") ?? string.Empty,
            ReadString(root, "status") ?? string.Empty,
            ReadDecimal(root, "transaction_amount"),
            ReadString(root, "currency_id") ?? "ARS",
            ReadString(root, "status_detail")
        );
    }

    private void ConfigureAuthorization()
    {
        var accessToken = _configuration["MercadoPago:AccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new PaymentGatewayException("MercadoPago:AccessToken no esta configurado.");
        }

        _httpClient.BaseAddress ??= new Uri("https://api.mercadopago.com/");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static string? FormatUrl(string? template, int orderId)
    {
        return string.IsNullOrWhiteSpace(template) ? null : template.Replace("{orderId}", orderId.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicCallbackUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https" &&
               !uri.IsLoopback &&
               !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static decimal ReadDecimal(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.TryGetDecimal(out var amount)
            ? amount
            : 0;
    }
}



