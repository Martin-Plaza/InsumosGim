using System.Net;
using System.Text;
using GymShop.Application.Abstractions;
using GymShop.Infrastructure.Configuration;
using GymShop.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymShop.Tests.Api;

public sealed class EmailDeliveryTests
{
    private const string Recipient = "private@example.com";
    private const string Code = "123456";
    private const string ApiKey = "secret-api-key";

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
    public async Task Resend_reports_request_accepted_without_claiming_delivery()
    {
        var (sender, logger) = CreateSender(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"provider-message-1\"}", Encoding.UTF8, "application/json")
        });

        var result = await ((IVerificationEmailSender)sender).SendAsync(Recipient, Code);

        Assert.True(result.AcceptedByProvider);
        Assert.Null(result.FailureType);
        Assert.Contains(logger.Messages, message => message.Contains("accepted by provider", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("delivered", StringComparison.OrdinalIgnoreCase));
        AssertLogsContainNoSecrets(logger);
    }

    [Fact]
    public async Task Resend_reports_http_rejection_with_status_only()
    {
        var (sender, logger) = CreateSender(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity));
        var result = await ((IPasswordResetEmailSender)sender).SendAsync(Recipient, Code);

        Assert.False(result.AcceptedByProvider);
        Assert.Equal(EmailSendFailureType.HttpRejected, result.FailureType);
        Assert.Equal(422, result.ProviderStatusCode);
        Assert.Contains(logger.Messages, message => message.Contains("422", StringComparison.Ordinal));
        AssertLogsContainNoSecrets(logger);
    }

    [Fact]
    public async Task Resend_reports_timeout()
    {
        var (sender, logger) = CreateSender(_ => throw new TaskCanceledException("simulated timeout"));
        var result = await ((IVerificationEmailSender)sender).SendAsync(Recipient, Code);

        Assert.False(result.AcceptedByProvider);
        Assert.Equal(EmailSendFailureType.Timeout, result.FailureType);
        AssertLogsContainNoSecrets(logger);
    }

    [Fact]
    public async Task Resend_reports_network_failure()
    {
        var (sender, logger) = CreateSender(_ => throw new HttpRequestException("simulated network error"));
        var result = await ((IPasswordResetEmailSender)sender).SendAsync(Recipient, Code);

        Assert.False(result.AcceptedByProvider);
        Assert.Equal(EmailSendFailureType.Network, result.FailureType);
        Assert.Contains(logger.Messages, message => message.Contains(nameof(HttpRequestException), StringComparison.Ordinal));
        AssertLogsContainNoSecrets(logger);
    }

    private static (ResendEmailSender Sender, CapturingLogger<ResendEmailSender> Logger) CreateSender(Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        var logger = new CapturingLogger<ResendEmailSender>();
        var client = new HttpClient(new StubHandler(response)) { BaseAddress = new Uri("https://api.resend.com/") };
        var sender = new ResendEmailSender(client, Options.Create(new EmailOptions
        {
            Provider = "Resend",
            ApiKey = ApiKey,
            FromAddress = "noreply@gymshop.invalid"
        }), logger);
        return (sender, logger);
    }

    private static void AssertLogsContainNoSecrets(CapturingLogger<ResendEmailSender> logger)
    {
        Assert.DoesNotContain(logger.Messages, message => message.Contains(Code, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(Recipient, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(ApiKey, StringComparison.Ordinal));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
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
