using GymShop.Application.Abstractions;
using GymShop.Infrastructure.Data;
using GymShop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GymShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetPostgresConnectionString(configuration);

        services.AddDbContext<GymShopDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<GymShopDbContext>());
        services.AddScoped<ITransactionManager, EfTransactionManager>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IVerificationEmailSender, MockVerificationEmailSender>();
        services.AddScoped<IPasswordResetEmailSender, MockPasswordResetEmailSender>();
        services.AddHttpClient<IExternalIdentityVerifier, GoogleIdentityVerifier>(client => client.BaseAddress = new Uri("https://oauth2.googleapis.com/"));
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(client =>
        {
            client.BaseAddress = new Uri("https://api.mercadopago.com/");
        });

        return services;
    }

    private static string GetPostgresConnectionString(IConfiguration configuration)
    {
        var configuredValue = configuration["DATABASE_URL"]
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            throw new InvalidOperationException(
                "Configure DATABASE_URL or ConnectionStrings:DefaultConnection.");
        }

        configuredValue = configuredValue.Trim();
        if (configuredValue.Length >= 2
            && ((configuredValue[0] == '"' && configuredValue[^1] == '"')
                || (configuredValue[0] == '\'' && configuredValue[^1] == '\'')))
        {
            configuredValue = configuredValue[1..^1];
        }

        if (!Uri.TryCreate(configuredValue, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
        {
            return configuredValue;
        }

        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("DATABASE_URL does not contain valid PostgreSQL credentials.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            SslMode = SslMode.Require,
            ChannelBinding = ChannelBinding.Require
        }.ConnectionString;
    }
}
