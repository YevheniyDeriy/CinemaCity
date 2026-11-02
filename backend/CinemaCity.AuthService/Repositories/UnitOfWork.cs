using CinemaCity.AuthService.Repositories;
using CinemaCity.AuthService.Data;

namespace CinemaCity.AuthService.Repositories;

public sealed class UnitOfWork(AuthDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfAppTransaction(transaction);
    }
}
