using GymShop.Application.Abstractions;
using GymShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Infrastructure.Data;

public class GymShopDbContext : DbContext, IApplicationDbContext
{
    public GymShopDbContext(DbContextOptions<GymShopDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200);
            entity.HasIndex(x => x.Name).IsUnique();

            entity.HasData(
                new Role { Id = 1, Name = "User", Description = "Cliente del e-commerce" },
                new Role { Id = 2, Name = "Admin", Description = "Administrador de productos y pedidos" },
                new Role { Id = 3, Name = "SuperAdmin", Description = "Administrador total del sistema" }
            );
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.Email).IsUnique();

            entity
                .HasOne(x => x.Role)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", table =>
            {
                table.HasCheckConstraint("CK_Products_Price_Positive", "[Price] > 0");
                table.HasCheckConstraint("CK_Products_Stock_NonNegative", "[Stock] >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(x => x.ShippingAddress).HasMaxLength(300).IsRequired();
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            entity
                .HasOne(x => x.User)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });


        modelBuilder.Entity<Cart>(entity =>
        {
            entity.ToTable("Carts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => x.UserId).IsUnique();

            entity
                .HasOne(x => x.User)
                .WithOne(x => x.Cart)
                .HasForeignKey<Cart>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("CartItems", table =>
            {
                table.HasCheckConstraint("CK_CartItems_Quantity_Positive", "[Quantity] > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();

            entity
                .HasOne(x => x.Cart)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(x => x.Product)
                .WithMany(x => x.CartItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems", table =>
            {
                table.HasCheckConstraint("CK_OrderItems_Quantity_Positive", "[Quantity] > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);

            entity
                .HasOne(x => x.Order)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(x => x.Product)
                .WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

