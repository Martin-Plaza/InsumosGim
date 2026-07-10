using GymShop.Domain.Entities;

namespace GymShop.Application.Abstractions.Repositories;

public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();
    void Add(TEntity entity);
    void AddRange(IEnumerable<TEntity> entities);
    void Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);
}

public interface IRoleRepository : IRepository<Role>
{
}

public interface IUserRepository : IRepository<User>
{
}

public interface IProductRepository : IRepository<Product>
{
}

public interface IOrderRepository : IRepository<Order>
{
}

public interface IOrderItemRepository : IRepository<OrderItem>
{
}

public interface ICartRepository : IRepository<Cart>
{
}

public interface ICartItemRepository : IRepository<CartItem>
{
}

public interface IPaymentRepository : IRepository<Payment>
{
}
