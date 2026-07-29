using Microsoft.EntityFrameworkCore;
using GymShop.Application.Abstractions;
using GymShop.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymShop.Infrastructure.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymShopDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync(cancellationToken);
        await SeedSuperAdminAsync(db, configuration, passwordHasher, cancellationToken);
        await SeedSampleProductsAsync(db, cancellationToken);
    }

    private static async Task SeedSuperAdminAsync(
        GymShopDbContext db,
        IConfiguration configuration,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("SeedSuperAdmin");
        var email = section["Email"]?.Trim().ToLowerInvariant();
        var password = section["Password"];
        var name = section["Name"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return;
        }

        var role = await db.Roles.SingleAsync(x => x.Name == "SuperAdmin", cancellationToken);
        db.Users.Add(new User
        {
            Email = email,
            Name = name,
            PasswordHash = passwordHasher.Hash(password),
            RoleId = role.Id,
            IsActive = true
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSampleProductsAsync(GymShopDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Products.AddRange(
            new()
            {
                Name = "Mancuerna 10kg",
                Description = "Mancuerna hexagonal para entrenamiento funcional.",
                Price = 25000,
                Stock = 15,
                ImageUrl = "/images/mancuerna.jpeg",
                IsActive = true
            },
            new()
            {
                Name = "Colchoneta fitness",
                Description = "Colchoneta antideslizante para ejercicios de piso.",
                Price = 18000,
                Stock = 20,
                ImageUrl = "/images/colchoneta.jpeg",
                IsActive = true
            }
        );

        await db.SaveChangesAsync(cancellationToken);
    }
}
