namespace CinemaCity.Application.Persistence;

public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
