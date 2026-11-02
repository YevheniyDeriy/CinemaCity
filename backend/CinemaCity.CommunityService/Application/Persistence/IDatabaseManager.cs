namespace CinemaCity.Application.Persistence;

public interface IDatabaseManager
{
    Task MigrateAsync(CancellationToken cancellationToken = default);

    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
}
