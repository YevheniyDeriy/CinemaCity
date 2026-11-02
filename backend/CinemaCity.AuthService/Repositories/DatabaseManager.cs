using CinemaCity.AuthService.Repositories;
using CinemaCity.AuthService.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.AuthService.Repositories;

public sealed class DatabaseManager(AuthDbContext dbContext) : IDatabaseManager
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
