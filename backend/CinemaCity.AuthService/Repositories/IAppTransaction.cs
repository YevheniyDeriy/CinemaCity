namespace CinemaCity.AuthService.Repositories;

public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
