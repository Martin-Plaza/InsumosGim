using System.Net;
using System.Text;
using GymShop.Application.Abstractions;
using GymShop.Domain.Entities;
using GymShop.Infrastructure.Configuration;
using GymShop.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace GymShop.Tests.Integration;

[Trait("Category", "Integration")]
[Trait("Category", "Gateway")]
public sealed class MercadoPagoGatewayHttpTests
{
    [Fact]
    public async Task Successful_preference_sends_authorization_and_idempotency_key()
    {
        var handler = new StubHttpHandler(_ => Json(HttpStatusCode.Created,
            """{"id":"pref-http-1","sandbox_init_point":"https://sandbox.example/checkout"}"""));
        var gateway = CreateGateway(handler);

        var result = await gateway.CreatePreferenceAsync(CreateOrder(), "idem-http-1");

        Assert.Equal("pref-http-1", result.ProviderPreferenceId);
        Assert.Equal("https://sandbox.example/checkout", result.CheckoutUrl);
        Assert.Equal("idem-http-1", handler.Requests.Single().Headers.GetValues("X-Idempotency-Key").Single());
        Assert.Equal("Bearer", handler.Requests.Single().Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task Http_4xx_and_5xx_are_reported_as_gateway_errors()
    {
        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError })
        {
            var gateway = CreateGateway(new StubHttpHandler(_ => Json(status, "{\"message\":\"provider error\"}")));
            var error = await Assert.ThrowsAsync<PaymentGatewayException>(() => gateway.CreatePreferenceAsync(CreateOrder(), "idem-error"));
            Assert.Contains(((int)status).ToString(), error.Message);
        }
    }

    [Fact]
    public async Task Timeout_and_invalid_json_are_exposed_without_fake_success()
    {
        var timeoutGateway = CreateGateway(new StubHttpHandler(_ => throw new TaskCanceledException("timeout")));
        await Assert.ThrowsAsync<TaskCanceledException>(() => timeoutGateway.CreatePreferenceAsync(CreateOrder(), "idem-timeout"));

        var invalidGateway = CreateGateway(new StubHttpHandler(_ => Json(HttpStatusCode.OK, "not-json")));
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => invalidGateway.CreatePreferenceAsync(CreateOrder(), "idem-json"));
    }

    [Fact]
    public async Task Retried_call_preserves_idempotency_key_and_refunded_response_is_parsed()
    {
        var handler = new StubHttpHandler(request => request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.Created, """{"id":"pref-retry","sandbox_init_point":"https://sandbox.example/retry"}""")
            : Json(HttpStatusCode.OK, """{"id":"pay-1","external_reference":"order-42","status":"refunded","transaction_amount":100,"currency_id":"ARS"}"""));
        var gateway = CreateGateway(handler);

        await gateway.CreatePreferenceAsync(CreateOrder(), "stable-idem-key");
        await gateway.CreatePreferenceAsync(CreateOrder(), "stable-idem-key");
        var refunded = await gateway.GetPaymentAsync("pay-1");

        Assert.Equal(2, handler.Requests.Count(x => x.Method == HttpMethod.Post));
        Assert.All(handler.Requests.Where(x => x.Method == HttpMethod.Post), request =>
            Assert.Equal("stable-idem-key", request.Headers.GetValues("X-Idempotency-Key").Single()));
        Assert.Equal("refunded", refunded.Status);
        Assert.Equal("order-42", refunded.ExternalReference);
    }

    private static MercadoPagoPaymentGateway CreateGateway(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.mercadopago.test/") };
        return new MercadoPagoPaymentGateway(client, Options.Create(new MercadoPagoOptions
        {
            Enabled = true, AccessToken = "TEST_ONLY_NOT_REAL", UseSandboxInitPoint = true
        }));
    }

    private static Order CreateOrder()
    {
        var user = new User { Email = "payer@test.com", Name = "Payer", PasswordHash = "not-used" };
        var order = new Order { Id = 42, User = user, Total = 100, ShippingAddress = "Test" };
        order.Items.Add(new OrderItem { ProductId = 1, ProductName = "Producto", Quantity = 1, UnitPrice = 100, Subtotal = 100 });
        return order;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(response(request));
        }
    }
}
