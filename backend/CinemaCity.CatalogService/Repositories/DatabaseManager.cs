using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.CatalogService.Repositories;

public sealed class DatabaseManager(CatalogDbContext dbContext) : IDatabaseManager
{
    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Database.CanConnectAsync(cancellationToken);
    }
}
