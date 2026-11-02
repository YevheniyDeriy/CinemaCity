using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CinemaCity.Infrastructure.Tmdb;

internal sealed class TmdbClient(
    HttpClient httpClient,
    IOptions<TmdbOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly TmdbOptions options = options.Value;

    public Task<TmdbPagedResponse<TmdbListItem>> GetPopularMoviesAsync(int page, CancellationToken cancellationToken)
    {
        return GetAsync<TmdbPagedResponse<TmdbListItem>>($"movie/popular?page={page}", cancellationToken);
    }

    public Task<TmdbPagedResponse<TmdbListItem>> GetPopularTvShowsAsync(int page, CancellationToken cancellationToken)
    {
        return GetAsync<TmdbPagedResponse<TmdbListItem>>($"tv/popular?page={page}", cancellationToken);
    }

    public Task<TmdbMovieDetails> GetMovieDetailsAsync(int tmdbId, CancellationToken cancellationToken, string language = "en-US")
    {
        return GetAsync<TmdbMovieDetails>($"movie/{tmdbId}", cancellationToken, language);
    }

    public Task<TmdbTvShowDetails> GetTvShowDetailsAsync(int tmdbId, CancellationToken cancellationToken, string language = "en-US")
    {
        return GetAsync<TmdbTvShowDetails>($"tv/{tmdbId}", cancellationToken, language);
    }

    public Task<TmdbCredits> GetMovieCreditsAsync(int tmdbId, CancellationToken cancellationToken)
    {
        return GetAsync<TmdbCredits>($"movie/{tmdbId}/credits", cancellationToken, language: null);
    }

    public Task<TmdbCredits> GetTvShowCreditsAsync(int tmdbId, CancellationToken cancellationToken)
    {
        return GetAsync<TmdbCredits>($"tv/{tmdbId}/credits", cancellationToken, language: null);
    }

    public Task<TmdbVideosResponse> GetMovieVideosAsync(int tmdbId, CancellationToken cancellationToken)
    {
        return GetAsync<TmdbVideosResponse>($"movie/{tmdbId}/videos", cancellationToken);
    }

    public Task<TmdbVideosResponse> GetTvShowVideosAsync(int tmdbId, CancellationToken cancellationToken)
    {
        return GetAsync<TmdbVideosResponse>($"tv/{tmdbId}/videos", cancellationToken);
    }

    public Task<TmdbTranslationsResponse> GetMovieTranslationsAsync(int tmdbId, CancellationToken cancellationToken)
    {
        return GetAsync<TmdbTranslationsResponse>($"movie/{tmdbId}/translations", cancellationToken, language: null);
    }

    public Task<TmdbTranslationsResponse> GetTvShowTranslationsAsync(int tmdbId, CancellationToken cancellationToken)
    {
        return GetAsync<TmdbTranslationsResponse>($"tv/{tmdbId}/translations", cancellationToken, language: null);
    }

    public Task<TmdbSeasonDetails> GetTvSeasonDetailsAsync(int tmdbId, int seasonNumber, CancellationToken cancellationToken, string language = "en-US")
    {
        return GetAsync<TmdbSeasonDetails>($"tv/{tmdbId}/season/{seasonNumber}", cancellationToken, language);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken, string? language = "en-US")
    {
        if (string.IsNullOrWhiteSpace(options.ApiToken))
        {
            throw new TmdbApiException("TMDB API token is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, AppendLanguage(path, language));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new TmdbRateLimitException("TMDB request limit was exceeded.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var reason = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
            throw new TmdbApiException($"TMDB request failed with {(int)response.StatusCode}: {reason}");
        }

        if (response.Content.Headers.ContentLength == 0)
        {
            throw new TmdbApiException("TMDB returned an empty response.");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload ?? throw new TmdbApiException("TMDB returned an empty response.");
    }

    private static string AppendLanguage(string path, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return path;
        }

        var separator = path.Contains('?') ? '&' : '?';
        return $"{path}{separator}language={Uri.EscapeDataString(language)}";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new NullableDateOnlyJsonConverter());
        return jsonOptions;
    }
}

internal class TmdbApiException(string message) : Exception(message);

internal sealed class TmdbRateLimitException(string message) : TmdbApiException(message);
