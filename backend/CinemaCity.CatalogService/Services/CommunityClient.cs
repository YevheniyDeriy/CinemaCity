using System.Security.Claims;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CinemaCity.CatalogService.Services;

/// <summary>Reads user-owned episode ratings from their owner; never reads the monolith database.</summary>
public sealed class CommunityClient(HttpClient client, IHttpContextAccessor accessor, IConfiguration configuration)
{
    public async Task<HashSet<Guid>> GetRatedEpisodeIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/community/rated-episodes") { Content = JsonContent.Create(new { ids = ids.ToArray() }) };
        request.Headers.TryAddWithoutValidation("X-Service-Key", configuration["Services:InternalKey"]);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HashSet<Guid>>(ct) ?? [];
    }
    public async Task<Dictionary<Guid, EpisodeUserRatingResponse>> GetEpisodeRatingsAsync(Guid seriesId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (user.Identity?.IsAuthenticated != true) return [];
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/internal/community/series/{seriesId}/ratings");
        var context = accessor.HttpContext;
        if (context is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToString());
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", context.TraceIdentifier);
        }
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Dictionary<Guid, EpisodeUserRatingResponse>>(ct) ?? [];
    }
}
