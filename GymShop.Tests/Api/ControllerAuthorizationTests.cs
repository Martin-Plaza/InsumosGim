using System.Reflection;
using GymShop.Api.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace GymShop.Tests.Api;

public class ControllerAuthorizationTests
{
    [Fact]
    public void Product_creation_is_restricted_to_admins()
    {
        var method = typeof(ProductsController).GetMethod(nameof(ProductsController.Create));
        var authorize = method?.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin,SuperAdmin", authorize!.Roles);
        Assert.DoesNotContain("User", SplitRoles(authorize.Roles));
    }

    [Fact]
    public void User_administration_is_restricted_to_superadmin()
    {
        var authorize = typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("SuperAdmin", authorize!.Roles);
        Assert.DoesNotContain("Admin", SplitRoles(authorize.Roles));
    }

    [Fact]
    public void MercadoPago_webhook_signature_validator_rejects_invalid_signature()
    {
        var isValid = MercadoPagoWebhookSignatureValidator.IsValid(
            "ts=123456,v1=invalid-signature",
            "request-id-1",
            "payment-id-1",
            "webhook-secret");

        Assert.False(isValid);
    }

    private static string[] SplitRoles(string? roles)
    {
        return roles?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    }
}
