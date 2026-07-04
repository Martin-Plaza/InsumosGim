using GymShop.Application.UseCases.Auth;
using GymShop.Application.UseCases.Orders;
using GymShop.Application.UseCases.Products;
using GymShop.Application.UseCases.Users;
using Microsoft.Extensions.DependencyInjection;

namespace GymShop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<ILoginUserUseCase, LoginUserUseCase>();
        services.AddScoped<IGetCurrentUserUseCase, GetCurrentUserUseCase>();

        services.AddScoped<IGetProductsUseCase, GetProductsUseCase>();
        services.AddScoped<IGetProductByIdUseCase, GetProductByIdUseCase>();
        services.AddScoped<ICreateProductUseCase, CreateProductUseCase>();
        services.AddScoped<IUpdateProductUseCase, UpdateProductUseCase>();
        services.AddScoped<IUpdateProductStockUseCase, UpdateProductStockUseCase>();
        services.AddScoped<IUpdateProductStatusUseCase, UpdateProductStatusUseCase>();

        services.AddScoped<ICreateOrderUseCase, CreateOrderUseCase>();
        services.AddScoped<IGetMyOrdersUseCase, GetMyOrdersUseCase>();
        services.AddScoped<IGetOrderByIdUseCase, GetOrderByIdUseCase>();
        services.AddScoped<IGetOrdersUseCase, GetOrdersUseCase>();
        services.AddScoped<IUpdateOrderStatusUseCase, UpdateOrderStatusUseCase>();

        services.AddScoped<IGetUsersUseCase, GetUsersUseCase>();
        services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();
        services.AddScoped<IUpdateUserRoleUseCase, UpdateUserRoleUseCase>();
        services.AddScoped<IUpdateUserStatusUseCase, UpdateUserStatusUseCase>();

        return services;
    }
}
