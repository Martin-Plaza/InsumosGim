using GymShop.Application.Abstractions;
using GymShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace GymShop.Infrastructure.Data;

public sealed class EfTransactionManager : ITransactionManager
{
    private readonly GymShopDbContext _db;

    public EfTransactionManager(GymShopDbContext db)
    {
        _db = db;
    }

    public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        return new EfApplicationTransaction(transaction);
    }

    private sealed class EfApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfApplicationTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
