using CinemaCity.Application.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace CinemaCity.Infrastructure.Persistence;

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
