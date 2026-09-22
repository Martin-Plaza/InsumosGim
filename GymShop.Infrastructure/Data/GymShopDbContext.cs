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
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("AuditEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OldValue).HasMaxLength(2000);
            entity.Property(x => x.NewValue).HasMaxLength(2000);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc });
            entity.HasIndex(x => x.CorrelationId);
            entity.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("StockMovements", table =>
            {
                table.HasCheckConstraint("CK_StockMovements_Quantity_NotZero", "\"Quantity\" <> 0");
                table.HasCheckConstraint("CK_StockMovements_Stocks_NonNegative", "\"PreviousStock\" >= 0 AND \"ResultingStock\" >= 0");
                table.HasCheckConstraint("CK_StockMovements_StockBalance", "\"ResultingStock\" = \"PreviousStock\" + \"Quantity\"");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => new { x.CreatedAtUtc, x.Id });
            entity.HasIndex(x => new { x.ProductId, x.CreatedAtUtc, x.Id });
            entity.HasIndex(x => new { x.OrderId, x.ProductId, x.Type })
                .IsUnique()
                .HasFilter("\"OrderId\" IS NOT NULL");
            entity.HasOne(x => x.Product).WithMany(x => x.StockMovements).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

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
            entity.Property(x => x.TokenVersion).HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => x.Email).IsUnique();

            entity
                .HasOne(x => x.Role)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmailVerificationCode>(entity =>
        {
            entity.ToTable("EmailVerificationCodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
            entity.HasOne(x => x.User).WithMany(x => x.EmailVerificationCodes).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetCode>(entity =>
        {
            entity.ToTable("PasswordResetCodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CodeHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
            entity.HasOne(x => x.User).WithMany(x => x.PasswordResetCodes).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserExternalLogin>(entity =>
        {
            entity.ToTable("UserExternalLogins");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProviderSubject).HasMaxLength(255).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => new { x.Provider, x.ProviderSubject }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.ExternalLogins).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.DisplayOrder });
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", table =>
            {
                table.HasCheckConstraint("CK_Products_Price_Positive", "\"Price\" > 0");
                table.HasCheckConstraint("CK_Products_Stock_NonNegative", "\"Stock\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.ImageUrl).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.CategoryId);
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.CouponCode).HasMaxLength(50);
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(x => x.ShippingAddress).HasMaxLength(300).IsRequired();
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.UpdatedAt).IsConcurrencyToken();

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("UX_Orders_UserId_Pending")
                .HasFilter("\"Status\" = 'Pending'");

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
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne(x => x.Coupon).WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.SetNull);

            entity
                .HasOne(x => x.User)
                .WithOne(x => x.Cart)
                .HasForeignKey<Cart>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.ToTable("Coupons", table =>
            {
                table.HasCheckConstraint("CK_Coupons_Value_Positive", "\"Value\" > 0");
                table.HasCheckConstraint("CK_Coupons_Percentage_Range", "\"Type\" <> 'Percentage' OR \"Value\" <= 100");
                table.HasCheckConstraint("CK_Coupons_Date_Range", "\"StartsAtUtc\" IS NULL OR \"EndsAtUtc\" IS NULL OR \"EndsAtUtc\" > \"StartsAtUtc\"");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Value).HasPrecision(18, 2);
            entity.Property(x => x.MinimumPurchase).HasPrecision(18, 2);
            entity.Property(x => x.MaximumDiscount).HasPrecision(18, 2);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.StartsAtUtc, x.EndsAtUtc });
        });

        modelBuilder.Entity<CouponRedemption>(entity =>
        {
            entity.ToTable("CouponRedemptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.OrderId).IsUnique();
            entity.HasIndex(x => new { x.CouponId, x.Status });
            entity.HasIndex(x => new { x.CouponId, x.UserId, x.Status });
            entity.HasOne(x => x.Coupon).WithMany(x => x.Redemptions).HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Order).WithOne(x => x.CouponRedemption).HasForeignKey<CouponRedemption>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("CartItems", table =>
            {
                table.HasCheckConstraint("CK_CartItems_Quantity_Positive", "\"Quantity\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
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


        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ExternalReference).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ProviderPreferenceId).HasMaxLength(100);
            entity.Property(x => x.ProviderPaymentId).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(x => x.CheckoutUrl).HasMaxLength(500);
            entity.Property(x => x.FailureReason).HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => x.ExternalReference);
            entity.HasIndex(x => new { x.Provider, x.ProviderPreferenceId }).HasFilter("\"ProviderPreferenceId\" IS NOT NULL");
            entity.HasIndex(x => new { x.Provider, x.ProviderPaymentId }).HasFilter("\"ProviderPaymentId\" IS NOT NULL");
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("UX_Payments_IdempotencyKey")
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
            entity.HasIndex(x => x.OrderId)
                .IsUnique()
                .HasDatabaseName("UX_Payments_OrderId_Active")
                .HasFilter("\"Status\" IN ('Creating', 'Pending')");

            entity
                .HasOne(x => x.Order)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems", table =>
            {
                table.HasCheckConstraint("CK_OrderItems_Quantity_Positive", "\"Quantity\" > 0");
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
