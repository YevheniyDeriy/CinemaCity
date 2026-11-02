using CinemaCity.AuthService.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace CinemaCity.AuthService.Repositories;

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
