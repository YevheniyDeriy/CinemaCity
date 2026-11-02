using CinemaCity.CatalogService.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace CinemaCity.CatalogService.Repositories;

internal sealed class EfAppTransaction(IDbContextTransaction transaction) : IAppTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return transaction.DisposeAsync();
    }
}
