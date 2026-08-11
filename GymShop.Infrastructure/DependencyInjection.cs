using GymShop.Application.Abstractions;
using GymShop.Infrastructure.Data;
using GymShop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<GymShopDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<GymShopDbContext>());
        services.AddScoped<ITransactionManager, EfTransactionManager>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IVerificationEmailSender, MockVerificationEmailSender>();
        services.AddHttpClient<IExternalIdentityVerifier, GoogleIdentityVerifier>(client => client.BaseAddress = new Uri("https://oauth2.googleapis.com/"));
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(client =>
        {
            client.BaseAddress = new Uri("https://api.mercadopago.com/");
        });

        return services;
    }
}
