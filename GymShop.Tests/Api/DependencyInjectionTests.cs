using GymShop.Application;
using GymShop.Application.Abstractions;
using GymShop.Application.UseCases.Payments;
using GymShop.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymShop.Tests.Api;

public class DependencyInjectionTests
{
    [Fact]
    public void Application_and_infrastructure_container_builds_without_dead_repository_registrations()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=GymShopDiTest;Trusted_Connection=True;TrustServerCertificate=True"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

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
}
