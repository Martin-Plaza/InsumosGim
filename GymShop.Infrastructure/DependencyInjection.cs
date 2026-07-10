using GymShop.Application.Abstractions;
using GymShop.Application.Abstractions.Repositories;
using GymShop.Infrastructure.Data;
using GymShop.Infrastructure.Repositories;
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
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<GymShopDbContext>());
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderItemRepository, OrderItemRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICartItemRepository, CartItemRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        services.AddHttpClient<IPaymentGateway, MercadoPagoPaymentGateway>(client =>
        {
            client.BaseAddress = new Uri("https://api.mercadopago.com/");
        });

        return services;
    }
}


