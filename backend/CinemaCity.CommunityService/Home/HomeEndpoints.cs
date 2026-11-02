using System.Security.Claims;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Home;

public static class HomeEndpoints
{
    public static IEndpointRouteBuilder MapHomeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/home/recommendations", GetRecommendationsAsync)
            .WithName("GetHomeRecommendations")
            .WithTags("Home");

        routes.MapGet("/api/home/popular-now", GetPopularNowAsync)
            .WithName("GetPopularNow")
            .WithTags("Home");

        return routes;
    }

    private static async Task<IResult> GetRecommendationsAsync(
        ClaimsPrincipal principal,
        IRepository<Movie> movieRepository,
        IRepository<WatchlistItem> watchlistRepository,
        IRepository<Rating> ratingRepository,
        IRepository<MovieCast> movieCastRepository,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 20);
        var userId = TryGetUserId(principal);

        if (userId is null)
        {
            var fallback = await GetFallbackRecommendationsAsync(movieRepository, normalizedLimit, cancellationToken);
            return Results.Ok(new HomeRailResponse("top-rated", fallback));
        }

        var watchedMovieIds = await watchlistRepository.Query()
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.Status == WatchlistStatus.Watched)
            .OrderByDescending(item => item.UpdatedAt ?? item.AddedAt)
            .Take(12)
            .Select(item => item.MovieId)
            .ToListAsync(cancellationToken);

        var ratedMovieIds = await ratingRepository.Query()
            .AsNoTracking()
            .Where(rating => rating.UserId == userId && rating.MovieId != null)
            .OrderByDescending(rating => rating.UpdatedAt ?? rating.CreatedAt)
            .Take(12)
            .Select(rating => rating.MovieId!.Value)
            .ToListAsync(cancellationToken);

        var seedMovieIds = watchedMovieIds
            .Concat(ratedMovieIds)
            .Distinct()
            .Take(16)
            .ToArray();

        if (seedMovieIds.Length == 0)
        {
            var fallback = await GetFallbackRecommendationsAsync(movieRepository, normalizedLimit, cancellationToken);
            return Results.Ok(new HomeRailResponse("top-rated", fallback));
        }

        var genreIds = await movieRepository.Query()
            .AsNoTracking()
            .Where(movie => seedMovieIds.Contains(movie.Id))
            .SelectMany(movie => movie.Genres.Select(genre => genre.Id))
            .GroupBy(id => id)
            .OrderByDescending(group => group.Count())
            .Take(8)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

        var personIds = await movieCastRepository.Query()
            .AsNoTracking()
            .Where(cast => seedMovieIds.Contains(cast.MovieId))
            .GroupBy(cast => cast.PersonId)
            .OrderByDescending(group => group.Count())
            .Take(4)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

        var candidateIds = await movieRepository.Query()
            .AsNoTracking()
            .Where(movie => !movie.IsDeleted && !seedMovieIds.Contains(movie.Id))
            .Where(movie =>
                movie.Genres.Any(genre => genreIds.Contains(genre.Id))
                || movie.Cast.Any(cast => personIds.Contains(cast.PersonId)))
            .Select(movie => new
            {
                movie.Id,
                movie.Title,
                GenreMatches = movie.Genres.Count(genre => genreIds.Contains(genre.Id)),
                PersonMatches = movie.Cast.Count(cast => personIds.Contains(cast.PersonId)),
                movie.AverageRating
            })
            .OrderByDescending(candidate => candidate.GenreMatches)
            .ThenByDescending(candidate => candidate.PersonMatches)
            .ThenByDescending(candidate => candidate.AverageRating)
            .ThenBy(candidate => candidate.Title)
            .Take(normalizedLimit)
            .Select(candidate => candidate.Id)
            .ToListAsync(cancellationToken);

        var candidateMovies = await movieRepository.Query()
            .AsNoTracking()
            .Include(movie => movie.Genres)
            .Where(movie => candidateIds.Contains(movie.Id))
            .ToListAsync(cancellationToken);

        var candidates = candidateIds
            .Select(id => candidateMovies.FirstOrDefault(movie => movie.Id == id))
            .Where(movie => movie is not null)
            .Select(movie => movie!)
            .ToList();

        if (candidates.Count < normalizedLimit)
        {
            var existingIds = candidates.Select(movie => movie.Id).Concat(seedMovieIds).ToArray();
            var fallback = await movieRepository.Query()
                .AsNoTracking()
                .Include(movie => movie.Genres)
                .Where(movie => !movie.IsDeleted && !existingIds.Contains(movie.Id))
                .OrderByDescending(movie => movie.AverageRating)
                .ThenByDescending(movie => movie.RatingsCount)
                .Take(normalizedLimit - candidates.Count)
                .ToListAsync(cancellationToken);

            candidates.AddRange(fallback);
        }

        return Results.Ok(new HomeRailResponse("personal", candidates.Select(ToCardResponse).ToArray()));
    }

    private static async Task<IResult> GetPopularNowAsync(
        IRepository<Movie> movieRepository,
        IRepository<Rating> ratingRepository,
        int days = 7,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedDays = Math.Clamp(days, 1, 30);
        var normalizedLimit = Math.Clamp(limit, 1, 20);
        var since = DateTimeOffset.UtcNow.AddDays(-normalizedDays);

        var popularIds = await ratingRepository.Query()
            .AsNoTracking()
            .Where(rating => rating.MovieId != null && rating.CreatedAt >= since)
            .GroupBy(rating => rating.MovieId!.Value)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Average(rating => rating.Score))
            .Take(normalizedLimit)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

        var movies = await movieRepository.Query()
            .AsNoTracking()
            .Include(movie => movie.Genres)
            .Where(movie => popularIds.Contains(movie.Id) && !movie.IsDeleted)
            .ToListAsync(cancellationToken);

        var ordered = popularIds
            .Select(id => movies.FirstOrDefault(movie => movie.Id == id))
            .Where(movie => movie is not null)
            .Select(movie => ToCardResponse(movie!))
            .ToList();

        if (ordered.Count < normalizedLimit)
        {
            var existingIds = ordered.Select(movie => movie.Id).ToArray();
            var fallbackMovies = await movieRepository.Query()
                .AsNoTracking()
                .Include(movie => movie.Genres)
                .Where(movie => !movie.IsDeleted && !existingIds.Contains(movie.Id))
                .OrderByDescending(movie => movie.RatingsCount)
                .ThenByDescending(movie => movie.AverageRating)
                .Take(normalizedLimit - ordered.Count)
                .ToListAsync(cancellationToken);

            ordered.AddRange(fallbackMovies.Select(ToCardResponse));
        }

        return Results.Ok(new HomeRailResponse($"popular-{normalizedDays}-days", ordered));
    }

    private static async Task<IReadOnlyCollection<HomeMovieCardResponse>> GetFallbackRecommendationsAsync(
        IRepository<Movie> movieRepository,
        int limit,
        CancellationToken cancellationToken)
    {
        var movies = await movieRepository.Query()
            .AsNoTracking()
            .Include(movie => movie.Genres)
            .Where(movie => !movie.IsDeleted)
            .OrderByDescending(movie => movie.AverageRating)
            .ThenByDescending(movie => movie.RatingsCount)
            .ThenByDescending(movie => movie.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return movies.Select(ToCardResponse).ToArray();
    }

    private static Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static HomeMovieCardResponse ToCardResponse(Movie movie)
    {
        return new HomeMovieCardResponse(
            movie.Id,
            movie.Title,
            movie.OriginalTitle,
            movie.ContentType,
            movie.ReleaseYear,
            movie.PosterUrl,
            movie.BackdropUrl,
            movie.AverageRating,
            movie.RatingsCount,
            movie.Genres
                .OrderBy(genre => genre.Name)
                .Select(genre => new HomeGenreResponse(genre.Id, genre.Name, genre.Slug))
                .ToArray());
    }

}

public sealed record HomeRailResponse(string Source, IReadOnlyCollection<HomeMovieCardResponse> Items);

public sealed record HomeGenreResponse(int Id, string Name, string Slug);

public sealed record HomeMovieCardResponse(
    Guid Id,
    string Title,
    string? OriginalTitle,
    ContentType ContentType,
    int ReleaseYear,
    string? PosterUrl,
    string? BackdropUrl,
    decimal AverageRating,
    int RatingsCount,
    IReadOnlyCollection<HomeGenreResponse> Genres);
