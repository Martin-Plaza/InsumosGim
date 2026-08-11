using GymShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<Product> Products { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<EmailVerificationCode> EmailVerificationCodes { get; }
    DbSet<UserExternalLogin> UserExternalLogins { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
