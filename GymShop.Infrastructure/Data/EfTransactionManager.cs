using GymShop.Application.Abstractions;
using GymShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    public async Task<IApplicationTransaction> BeginUserAdministrationTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // One database-wide transactional mutex makes the count-and-update invariant atomic
            // across every API instance. PostgreSQL releases it automatically on commit/rollback.
            await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(87324519)", cancellationToken);
            return new EfApplicationTransaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
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
            return CommitCoreAsync(cancellationToken);
        }

        private async Task CommitCoreAsync(CancellationToken cancellationToken)
        {
            try { await _transaction.CommitAsync(cancellationToken); }
            catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                throw new DbUpdateConcurrencyException("La operación concurrente debe reintentarse.", exception);
            }
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
