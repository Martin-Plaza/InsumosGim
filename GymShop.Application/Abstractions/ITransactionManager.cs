namespace GymShop.Application.Abstractions;

public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface ITransactionManager
{
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
