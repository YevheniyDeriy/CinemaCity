using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Data;

namespace CinemaCity.CatalogService.Repositories;

public sealed class UnitOfWork(CatalogDbContext dbContext) : IUnitOfWork
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
