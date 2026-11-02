using System.Net.Http.Json;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Integration;

/// <summary>Retries delivery after a catalog outage without rolling back an already saved rating.</summary>
public sealed class StatisticsOutboxWorker(IServiceScopeFactory scopes, IHttpClientFactory clients, IConfiguration configuration, ILogger<StatisticsOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Services:Enabled", false)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTimeOffset.UtcNow;
                var messages = await db.CatalogStatisticsMessages.Where(m => m.SentAt == null && (m.NextAttemptAt == null || m.NextAttemptAt <= now)).OrderBy(m => m.Id).Take(50).ToListAsync(stoppingToken);
                foreach (var message in messages)
                {
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Put, "/internal/catalog/statistics")
                        { Content = JsonContent.Create(new { message.Kind, id = message.TargetId, message.AverageRating, message.RatingsCount, version = message.Id }) };
                        request.Headers.TryAddWithoutValidation("X-Service-Key", configuration["Services:InternalKey"]);
                        using var response = await clients.CreateClient("CatalogOwner").SendAsync(request, stoppingToken);
                        response.EnsureSuccessStatusCode();
                        message.SentAt = DateTimeOffset.UtcNow;
                    }
                    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
                    {
                        message.Attempts++;
                        message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 1 << Math.Min(message.Attempts, 5)));
                        logger.LogWarning("Catalog statistics delivery deferred for {Kind} {Id}; attempt {Attempt}", message.Kind, message.TargetId, message.Attempts);
                    }
                }
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested) { logger.LogError(exception, "Statistics delivery loop failed; will retry."); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
