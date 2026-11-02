namespace CinemaCity.CatalogService.Repositories;

public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
