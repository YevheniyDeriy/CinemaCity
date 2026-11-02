using CinemaCity.Application.Persistence;
using CinemaCity.Infrastructure.Data;
using CinemaCity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CinemaCity.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext dbContext, IConfiguration configuration) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (configuration.GetValue("Services:Enabled", false))
        {
            dbContext.ChangeTracker.DetectChanges();
            foreach (var entry in dbContext.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Modified).ToArray())
            {
                if (entry.Entity is Movie movie && (entry.Property(nameof(Movie.AverageRating)).IsModified || entry.Property(nameof(Movie.RatingsCount)).IsModified))
                    dbContext.CatalogStatisticsMessages.Add(new CatalogStatisticsMessage { Kind = "Movie", TargetId = movie.Id, AverageRating = movie.AverageRating, RatingsCount = movie.RatingsCount });
                if (entry.Entity is Episode episode && (entry.Property(nameof(Episode.AverageRating)).IsModified || entry.Property(nameof(Episode.RatingsCount)).IsModified))
                    dbContext.CatalogStatisticsMessages.Add(new CatalogStatisticsMessage { Kind = "Episode", TargetId = episode.Id, AverageRating = episode.AverageRating, RatingsCount = episode.RatingsCount });
            }
        }
        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IAppTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is not null) return new EnlistedTransaction();
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfAppTransaction(transaction);
    }
    private sealed class EnlistedTransaction : IAppTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
