using GymShop.Application;
using GymShop.Application.Abstractions;
using GymShop.Application.UseCases.Payments;
using GymShop.Infrastructure;
using GymShop.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;

namespace GymShop.Tests.Api;

public class DependencyInjectionTests
{
    [Fact]
    public void Application_and_infrastructure_container_builds_without_dead_repository_registrations()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=GymShopDiTest;Username=postgres;Password=postgres"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration, new TestHostEnvironment());

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.Namespace?.Contains("Repositories", StringComparison.Ordinal) == true);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITransactionManager>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICreatePaymentUseCase>());
    }

    [Fact]
    public void Oca_can_replace_own_fleet_through_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=GymShopDiTest;Username=postgres;Password=postgres",
                ["Shipping:OwnFleetEnabled"] = "false",
                ["Oca:Enabled"] = "true",
                ["Oca:Environment"] = "Qa",
                ["Oca:Cuit"] = "30-53625919-4",
                ["Oca:Operativa"] = "64665"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddApplication();
        services.AddInfrastructure(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var shippingProviders = provider.GetServices<IShippingProvider>().ToList();
        var shippingProvider = Assert.Single(shippingProviders);
        Assert.IsType<OcaShippingProvider>(shippingProvider);
    }

    [Fact]
    public void Correo_argentino_can_replace_other_shipping_providers_through_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=GymShopDiTest;Username=postgres;Password=postgres",
                ["Shipping:OwnFleetEnabled"] = "false",
                ["CorreoArgentino:Enabled"] = "true",
                ["CorreoArgentino:Environment"] = "Qa",
                ["CorreoArgentino:ApiUsername"] = "api-user",
                ["CorreoArgentino:ApiPassword"] = "api-password",
                ["CorreoArgentino:CustomerId"] = "customer-id"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddApplication();
        services.AddInfrastructure(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var shippingProviders = provider.GetServices<IShippingProvider>().ToList();
        var shippingProvider = Assert.Single(shippingProviders);
        Assert.IsType<CorreoArgentinoShippingProvider>(shippingProvider);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "GymShop.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
