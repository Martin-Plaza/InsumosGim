using System.Net;
using GymShop.Application.Abstractions;
using GymShop.Infrastructure.Configuration;
using GymShop.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymShop.Tests.Api;

public sealed class EmailDeliveryTests
{
    [Fact]
    public void Mock_provider_is_rejected_outside_development()
    {
        var result = new EmailOptionsValidator("Production").Validate(null, new EmailOptions { Provider = "Mock" });

        Assert.False(result.Succeeded);
        Assert.Contains("only allowed", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resend_requires_api_key_and_sender_in_deployed_environments()
    {
        var result = new EmailOptionsValidator("Production").Validate(null, new EmailOptions { Provider = "Resend" });

        Assert.False(result.Succeeded);
        Assert.Contains("Email:ApiKey", result.FailureMessage);
        Assert.Contains("Email:FromAddress", result.FailureMessage);
    }

    [Fact]
    public async Task Resend_never_returns_or_logs_verification_code_or_recipient()
    {
        const string email = "private@example.com";
        const string code = "123456";
        var logger = new CapturingLogger<ResendEmailSender>();
        using var client = new HttpClient(new StubHandler(HttpStatusCode.BadGateway))
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var sender = new ResendEmailSender(client, Options.Create(new EmailOptions
        {
            Provider = "Resend",
            ApiKey = "test-key",
            FromAddress = "noreply@gymshop.invalid"
        }), logger);

        var exposedCode = await ((IVerificationEmailSender)sender).SendAsync(email, code);

        Assert.Null(exposedCode);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(code, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(email, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
