using GymShop.Application.UseCases.Auth;
using GymShop.Application.UseCases.Carts;
using GymShop.Application.UseCases.Orders;
using GymShop.Application.UseCases.Payments;
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

        services.AddScoped<IGetMyOrdersUseCase, GetMyOrdersUseCase>();
        services.AddScoped<IGetOrderByIdUseCase, GetOrderByIdUseCase>();
        services.AddScoped<IGetOrdersUseCase, GetOrdersUseCase>();
        services.AddScoped<IUpdateOrderStatusUseCase, UpdateOrderStatusUseCase>();
        services.AddScoped<ICancelOrderUseCase, CancelOrderUseCase>();
        services.AddScoped<IExpirePendingOrdersUseCase, ExpirePendingOrdersUseCase>();

        services.AddScoped<ICreatePaymentUseCase, CreatePaymentUseCase>();
        services.AddScoped<ICreateCurrentPaymentUseCase, CreateCurrentPaymentUseCase>();
        services.AddScoped<IGetPaymentByIdUseCase, GetPaymentByIdUseCase>();
        services.AddScoped<IGetOrderPaymentsUseCase, GetOrderPaymentsUseCase>();
        services.AddScoped<IUpdatePaymentStatusUseCase, UpdatePaymentStatusUseCase>();
        services.AddScoped<IHandlePaymentWebhookUseCase, HandlePaymentWebhookUseCase>();

        services.AddScoped<IGetCartUseCase, GetCartUseCase>();
        services.AddScoped<IAddCartItemUseCase, AddCartItemUseCase>();
        services.AddScoped<IUpdateCartItemUseCase, UpdateCartItemUseCase>();
        services.AddScoped<IRemoveCartItemUseCase, RemoveCartItemUseCase>();
        services.AddScoped<IClearCartUseCase, ClearCartUseCase>();
        services.AddScoped<ICheckoutCartUseCase, CheckoutCartUseCase>();

        services.AddScoped<IGetUsersUseCase, GetUsersUseCase>();
        services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();
        services.AddScoped<IUpdateUserRoleUseCase, UpdateUserRoleUseCase>();
        services.AddScoped<IUpdateUserStatusUseCase, UpdateUserStatusUseCase>();

        return services;
    }
}







