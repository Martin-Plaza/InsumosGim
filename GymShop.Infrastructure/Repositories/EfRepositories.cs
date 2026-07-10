using GymShop.Application.Abstractions.Repositories;
using GymShop.Domain.Entities;
using GymShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymShop.Infrastructure.Repositories;

public abstract class EfRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected EfRepository(GymShopDbContext db)
    {
        Db = db;
    }

    protected GymShopDbContext Db { get; }
    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    public IQueryable<TEntity> Query() => Set;

    public void Add(TEntity entity) => Set.Add(entity);

    public void AddRange(IEnumerable<TEntity> entities) => Set.AddRange(entities);

    public void Remove(TEntity entity) => Set.Remove(entity);

    public void RemoveRange(IEnumerable<TEntity> entities) => Set.RemoveRange(entities);
}

public sealed class RoleRepository : EfRepository<Role>, IRoleRepository
{
    public RoleRepository(GymShopDbContext db) : base(db)
    {
    }
}

public sealed class UserRepository : EfRepository<User>, IUserRepository
{
    public UserRepository(GymShopDbContext db) : base(db)
    {
    }
}

public sealed class ProductRepository : EfRepository<Product>, IProductRepository
{
    public ProductRepository(GymShopDbContext db) : base(db)
    {
    }
}

public sealed class OrderRepository : EfRepository<Order>, IOrderRepository
{
    public OrderRepository(GymShopDbContext db) : base(db)
    {
    }
}

public sealed class OrderItemRepository : EfRepository<OrderItem>, IOrderItemRepository
{
    public OrderItemRepository(GymShopDbContext db) : base(db)
    {
    }
}

public sealed class CartRepository : EfRepository<Cart>, ICartRepository
{
    public CartRepository(GymShopDbContext db) : base(db)
    {
    }
}

public sealed class CartItemRepository : EfRepository<CartItem>, ICartItemRepository
{
    public CartItemRepository(GymShopDbContext db) : base(db)
    {
    }
}

public sealed class PaymentRepository : EfRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(GymShopDbContext db) : base(db)
    {
    }
}
