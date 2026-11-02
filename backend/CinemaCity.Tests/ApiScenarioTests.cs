using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CinemaCity.Tests;

public sealed class ApiScenarioTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CinemaCityApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.SeedAsync();
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DropDatabaseAsync();
        _factory.Dispose();
    }

    [Fact]
    public async Task Auth_RegisterLoginAndMe_ReturnJwtSession()
    {
        var email = UniqueEmail("auth");

        var register = await RegisterAsync(email);

        Assert.False(string.IsNullOrWhiteSpace(register.AccessToken));
        Assert.StartsWith("u_", register.User.Username);
        Assert.Equal(email, register.User.Email);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", register.AccessToken);
        var meResponse = await _client.SendAsync(meRequest);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await ReadJsonAsync<AuthUserResponse>(meResponse);
        Assert.Equal(register.User.Id, me.Id);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = TestConstants.Password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await ReadJsonAsync<AuthResponse>(loginResponse);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
    }

    [Fact]
    public async Task PublicProfile_ReturnsOnlyPublicListsAndApprovedReviews()
    {
        var listsResponse = await _client.GetAsync($"/api/users/{_factory.SeedIds.PublicUserId}/lists");
        Assert.Equal(HttpStatusCode.OK, listsResponse.StatusCode);

        var lists = await ReadJsonAsync<List<MovieListSummaryResponse>>(listsResponse);
        var list = Assert.Single(lists);
        Assert.Equal(_factory.SeedIds.PublicListId, list.Id);
        Assert.True(list.IsPublic);

        var reviewsResponse = await _client.GetAsync($"/api/users/{_factory.SeedIds.PublicUserId}/reviews");
        Assert.Equal(HttpStatusCode.OK, reviewsResponse.StatusCode);

        var reviews = await ReadJsonAsync<List<PublicUserReviewResponse>>(reviewsResponse);
        var review = Assert.Single(reviews);
        Assert.Equal(_factory.SeedIds.MovieId, review.MovieId);
        Assert.Contains("public review", review.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RatingEpisode_AddsSeriesToWatchedAndReturnsUserRating()
    {
        var auth = await RegisterAsync(UniqueEmail("episode"));
        Authorize(auth.AccessToken);

        var ratingResponse = await _client.PostAsJsonAsync("/api/ratings", new
        {
            episodeId = _factory.SeedIds.EpisodeId,
            score = 9
        });

        Assert.Equal(HttpStatusCode.Created, ratingResponse.StatusCode);

        var watchedResponse = await _client.GetAsync("/api/profile/watchlist?status=Watched");
        Assert.Equal(HttpStatusCode.OK, watchedResponse.StatusCode);

        var watched = await ReadJsonAsync<List<WatchlistItemResponse>>(watchedResponse);
        Assert.Contains(watched, item => item.MovieId == _factory.SeedIds.SeriesId);

        var seasonsResponse = await _client.GetAsync($"/api/movies/{_factory.SeedIds.SeriesId}/seasons");
        Assert.Equal(HttpStatusCode.OK, seasonsResponse.StatusCode);

        var seasons = await ReadJsonAsync<List<SeasonResponse>>(seasonsResponse);
        var episode = Assert.Single(Assert.Single(seasons).Episodes);
        Assert.NotNull(episode.UserRating);
        Assert.Equal(9m, episode.UserRating.Score);

        var duplicateResponse = await _client.PostAsJsonAsync("/api/ratings", new
        {
            episodeId = _factory.SeedIds.EpisodeId,
            score = 8
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task AdminMovieCreate_RejectsNegativeCastSortOrder()
    {
        var admin = await LoginAsync(TestConstants.AdminEmail);
        Authorize(admin.AccessToken);

        var response = await _client.PostAsJsonAsync("/api/admin/movies", new
        {
            title = "Invalid Sort Order Movie",
            originalTitle = "Invalid Sort Order Movie",
            contentType = "Movie",
            releaseYear = 2026,
            duration = 100,
            description = "Invalid admin payload.",
            posterUrl = (string?)null,
            backdropUrl = (string?)null,
            trailerYoutubeId = (string?)null,
            genreIds = Array.Empty<int>(),
            countryIds = Array.Empty<int>(),
            languageIds = Array.Empty<int>(),
            cast = new[]
            {
                new
                {
                    personId = Guid.NewGuid(),
                    creditType = "Actor",
                    roleName = "Tester",
                    sortOrder = -1
                }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cast sort order cannot be negative.", body);
    }

    private async Task<AuthResponse> RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"u_{Guid.NewGuid():N}"[..20],
            email,
            password = TestConstants.Password,
            confirmPassword = TestConstants.Password
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync<AuthResponse>(response);
    }

    private async Task<AuthResponse> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = TestConstants.Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync<AuthResponse>(response);
    }

    private void Authorize(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(result);
        return result;
    }

    private static string UniqueEmail(string prefix)
    {
        return $"{prefix}.{Guid.NewGuid():N}@tests.local";
    }

    private sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AuthUserResponse User);

    private sealed record AuthUserResponse(Guid Id, string Username, string Email, string Role, string? AvatarUrl);

    private sealed record MovieListSummaryResponse(
        Guid Id,
        string Name,
        string? Description,
        bool IsPublic,
        int ItemsCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record PublicUserReviewResponse(
        Guid Id,
        Guid MovieId,
        string MovieTitle,
        string? MoviePosterUrl,
        string ContentType,
        int ReleaseYear,
        string Content,
        bool IsSpoiler,
        int LikesCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private sealed record WatchlistItemResponse(
        Guid Id,
        Guid MovieId,
        string Title,
        string? OriginalTitle,
        string ContentType,
        int ReleaseYear);

    private sealed record SeasonResponse(
        Guid Id,
        Guid SeriesId,
        int SeasonNumber,
        string? Title,
        string? Description,
        int? ReleaseYear,
        IReadOnlyCollection<EpisodeResponse> Episodes);

    private sealed record EpisodeResponse(
        Guid Id,
        Guid SeasonId,
        int EpisodeNumber,
        string Title,
        string? Description,
        DateOnly? AirDate,
        int? Duration,
        decimal AverageRating,
        int RatingsCount,
        EpisodeUserRatingResponse? UserRating);

    private sealed record EpisodeUserRatingResponse(Guid Id, decimal Score);

}
