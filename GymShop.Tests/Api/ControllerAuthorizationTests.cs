using System.Reflection;
using GymShop.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public void Audit_query_is_restricted_to_superadmin()
    {
        var authorize = typeof(AuditController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal("SuperAdmin", authorize!.Roles);
        Assert.DoesNotContain("Admin", SplitRoles(authorize.Roles));
        Assert.Single(typeof(AuditController).GetMethods(), x => x.GetCustomAttribute<HttpGetAttribute>() is not null);
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

    [Fact]
    public void Payment_creation_exposes_only_explicit_order_route()
    {
        var postRoutes = typeof(PaymentsController).GetMethods()
            .SelectMany(method => method.GetCustomAttributes<HttpPostAttribute>())
            .Select(attribute => attribute.Template)
            .ToList();

        Assert.Contains("/api/orders/{orderId:int}/payments", postRoutes);
        Assert.DoesNotContain(postRoutes, route => route?.Contains("current", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string[] SplitRoles(string? roles)
    {
        return roles?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    }
}
