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
        await SeedCategoriesAsync(db, cancellationToken);
        await SeedSampleProductsAsync(db, cancellationToken);
    }

    private static async Task SeedCategoriesAsync(GymShopDbContext db, CancellationToken cancellationToken)
    {
        var categories = new[]
        {
            new Category { Name = "Fuerza", Slug = "fuerza", Description = "Pesas y equipamiento para desarrollar fuerza.", DisplayOrder = 10 },
            new Category { Name = "Entrenamiento funcional", Slug = "entrenamiento-funcional", Description = "Accesorios versátiles para movilidad y potencia.", DisplayOrder = 20 },
            new Category { Name = "Yoga y movilidad", Slug = "yoga-movilidad", Description = "Productos para movilidad, recuperación y ejercicios de piso.", DisplayOrder = 30 },
            new Category { Name = "Cardio", Slug = "cardio", Description = "Equipamiento para acondicionamiento cardiovascular.", DisplayOrder = 40 }
        };

        foreach (var category in categories)
            if (!await db.Categories.AnyAsync(x => x.Slug == category.Slug, cancellationToken)) db.Categories.Add(category);

        await db.SaveChangesAsync(cancellationToken);
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
            IsActive = true,
            EmailVerifiedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSampleProductsAsync(GymShopDbContext db, CancellationToken cancellationToken)
    {
        var categoryIds = await db.Categories.ToDictionaryAsync(x => x.Slug, x => x.Id, cancellationToken);
        var samples = new Product[]
        {
            new()
            {
                Name = "Mancuerna 10kg",
                Description = "Mancuerna hexagonal para entrenamiento funcional.",
                Price = 25000,
                Stock = 15,
                ImageUrl = "/images/products/mancuerna-10kg.webp",
                IsActive = true,
                CategoryId = categoryIds["fuerza"]
            },
            new()
            {
                Name = "Colchoneta fitness",
                Description = "Colchoneta antideslizante para ejercicios de piso.",
                Price = 18000,
                Stock = 20,
                ImageUrl = "/images/products/colchoneta-fitness.webp",
                IsActive = true,
                CategoryId = categoryIds["yoga-movilidad"]
            },
            new()
            {
                Name = "Kettlebell 16kg",
                Description = "Pesa rusa de hierro con agarre amplio para fuerza y potencia.",
                Price = 42000,
                Stock = 12,
                ImageUrl = "/images/products/kettlebell-16kg.webp",
                IsActive = true,
                CategoryId = categoryIds["fuerza"]
            },
            new()
            {
                Name = "Bandas de resistencia",
                Description = "Set de cinco bandas textiles con distintas intensidades.",
                Price = 22000,
                Stock = 25,
                ImageUrl = "/images/products/bandas-resistencia.webp",
                IsActive = true,
                CategoryId = categoryIds["entrenamiento-funcional"]
            },
            new()
            {
                Name = "Banco regulable",
                Description = "Banco de entrenamiento ajustable para rutinas de fuerza.",
                Price = 185000,
                Stock = 8,
                ImageUrl = "/images/products/banco-regulable.webp",
                IsActive = true,
                CategoryId = categoryIds["fuerza"]
            },
            new()
            {
                Name = "Soga de velocidad",
                Description = "Soga liviana con cable rápido y mangos ergonómicos.",
                Price = 16000,
                Stock = 30,
                ImageUrl = "/images/products/soga-velocidad.webp",
                IsActive = true,
                CategoryId = categoryIds["cardio"]
            }
        };

        foreach (var sample in samples)
        {
            var existing = await db.Products.SingleOrDefaultAsync(x => x.Name == sample.Name, cancellationToken);
            if (existing is null)
            {
                db.Products.Add(sample);
            }
            else if (string.IsNullOrWhiteSpace(existing.ImageUrl) || existing.ImageUrl is "/images/mancuerna.jpeg" or "/images/colchoneta.jpeg")
            {
                existing.ImageUrl = sample.ImageUrl;
            }
            if (existing is not null && existing.CategoryId is null) existing.CategoryId = sample.CategoryId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
