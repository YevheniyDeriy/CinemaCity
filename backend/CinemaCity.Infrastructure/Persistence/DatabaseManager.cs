using CinemaCity.Application.Persistence;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.Infrastructure.Persistence;

public sealed class DatabaseManager(AppDbContext dbContext) : IDatabaseManager
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
