using GymShop.Application.Abstractions;
using GymShop.Domain.Entities;
using GymShop.Infrastructure.Data;
using GymShop.Infrastructure.Services;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GymShop.Tests.Integration;

internal sealed class GymShopWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestJwtSecret = "GymShop_HTTP_Tests_Secret_64_Characters_Long_And_Not_Production_12345";
    private readonly IReadOnlyDictionary<string, string?> _overrides;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly bool _useInMemoryDatabase;
    private readonly string _inMemoryDatabaseName = $"GymShopHttp_{Guid.NewGuid():N}";
    private readonly IServiceProvider _inMemoryProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    public GymShopWebApplicationFactory(
        IReadOnlyDictionary<string, string?>? overrides = null,
        Action<IServiceCollection>? configureServices = null,
        bool useInMemoryDatabase = false)
    {
        _overrides = overrides ?? new Dictionary<string, string?>();
        _configureServices = configureServices;
        _useInMemoryDatabase = useInMemoryDatabase;
        var baseConnection = Environment.GetEnvironmentVariable("GYMSHOP_TEST_POSTGRES") ??
            "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
        var builder = new NpgsqlConnectionStringBuilder(baseConnection)
        {
            Database = $"GymShopHttp_{Guid.NewGuid():N}"
        };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
        });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Jwt:Issuer"] = "GymShop.HttpTests",
                ["Jwt:Audience"] = "GymShop.HttpTests.Client",
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:ExpirationMinutes"] = "60",
                ["RateLimiting:Enabled"] = "false",
                ["MercadoPago:Enabled"] = "false",
                ["ReverseProxy:Enabled"] = "false"
            };
            foreach (var item in _overrides) values[item.Key] = item.Value;
            configuration.AddInMemoryCollection(values);
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<GymShopDbContext>>();
            services.RemoveAll<GymShopDbContext>();
            if (_useInMemoryDatabase) services.RemoveAll<IDatabaseProvider>();
            services.AddDbContext<GymShopDbContext>(options =>
            {
                if (_useInMemoryDatabase) options
                    .UseInMemoryDatabase(_inMemoryDatabaseName)
                    .UseInternalServiceProvider(_inMemoryProvider);
                else options.UseNpgsql(ConnectionString);
            });
            _configureServices?.Invoke(services);
        });
    }

    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    public async Task InitializeAsync()
    {
        _ = Server;
        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<GymShopDbContext>().Database;
        if (_useInMemoryDatabase) await database.EnsureCreatedAsync();
        else await database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<GymShopDbContext>().Database.EnsureDeletedAsync();
        }
        finally
        {
            await base.DisposeAsync();
        }
    }

    public async Task<User> SeedUserAsync(string email, string roleName, string password = "clave123")
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GymShopDbContext>();
        var role = await db.Roles.SingleAsync(x => x.Name == roleName);
        var user = new User
        {
            Email = email.ToLowerInvariant(), Name = roleName + " HTTP", PasswordHash = new PasswordHasher().Hash(password), EmailVerifiedAt = DateTime.UtcNow,
            RoleId = role.Id, IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<string> LoginAsync(HttpClient client, string email, string password = "clave123")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()!;
    }
}
