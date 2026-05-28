using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Search;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/search", SearchAsync)
            .WithName("GlobalSearch")
            .WithTags("Search");

        return routes;
    }

    private static async Task<IResult> SearchAsync(
        AppDbContext dbContext,
        string? q,
        string? scope = "all",
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var query = q?.Trim();
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "all" : scope.Trim().ToLowerInvariant();
        var normalizedLimit = Math.Clamp(limit, 1, 20);

        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.Ok(SearchResponse.Empty(normalizedScope));
        }

        var search = query.ToLowerInvariant();
        var includeMovies = normalizedScope is "all" or "movie" or "series" or "title";
        var includeEpisodes = normalizedScope is "all" or "episode";
        var includePeople = normalizedScope is "all" or "person" or "celebrity";

        var movieQuery = dbContext.Movies
            .AsNoTracking()
            .Where(movie => !movie.IsDeleted);

        if (normalizedScope == "movie")
        {
            movieQuery = movieQuery.Where(movie => movie.ContentType == ContentType.Movie);
        }
        else if (normalizedScope == "series")
        {
            movieQuery = movieQuery.Where(movie => movie.ContentType == ContentType.Series);
        }

        var movies = includeMovies
            ? await movieQuery
                .Where(movie =>
                    movie.Title.ToLower().Contains(search)
                    || (movie.OriginalTitle != null && movie.OriginalTitle.ToLower().Contains(search)))
                .OrderByDescending(movie => movie.AverageRating)
                .ThenBy(movie => movie.Title)
                .Take(normalizedLimit)
                .Select(movie => new SearchMovieResult(
                    movie.Id,
                    movie.Title,
                    movie.OriginalTitle,
                    movie.ContentType,
                    movie.ReleaseYear,
                    movie.PosterUrl,
                    movie.AverageRating))
                .ToListAsync(cancellationToken)
            : [];

        var episodes = includeEpisodes
            ? await dbContext.Episodes
                .AsNoTracking()
                .Include(episode => episode.Season)
                    .ThenInclude(season => season.Series)
                .Where(episode => episode.Title.ToLower().Contains(search))
                .OrderByDescending(episode => episode.AverageRating)
                .ThenBy(episode => episode.Title)
                .Take(normalizedLimit)
                .Select(episode => new SearchEpisodeResult(
                    episode.Id,
                    episode.Title,
                    episode.Season.SeriesId,
                    episode.Season.Series.Title,
                    episode.Season.SeasonNumber,
                    episode.EpisodeNumber,
                    episode.AirDate,
                    episode.AverageRating))
                .ToListAsync(cancellationToken)
            : [];

        var people = includePeople
            ? await dbContext.Persons
                .AsNoTracking()
                .Where(person => person.FullName.ToLower().Contains(search))
                .OrderBy(person => person.FullName)
                .Take(normalizedLimit)
                .Select(person => new SearchPersonResult(
                    person.Id,
                    person.FullName,
                    person.PhotoUrl,
                    person.BirthPlace))
                .ToListAsync(cancellationToken)
            : [];

        return Results.Ok(new SearchResponse(normalizedScope, query, movies, episodes, people));
    }
}

public sealed record SearchResponse(
    string Scope,
    string Query,
    IReadOnlyCollection<SearchMovieResult> Movies,
    IReadOnlyCollection<SearchEpisodeResult> Episodes,
    IReadOnlyCollection<SearchPersonResult> People)
{
    public static SearchResponse Empty(string scope)
    {
        return new SearchResponse(scope, string.Empty, [], [], []);
    }
}

public sealed record SearchMovieResult(
    Guid Id,
    string Title,
    string? OriginalTitle,
    ContentType ContentType,
    int ReleaseYear,
    string? PosterUrl,
    decimal AverageRating);

public sealed record SearchEpisodeResult(
    Guid Id,
    string Title,
    Guid SeriesId,
    string SeriesTitle,
    int SeasonNumber,
    int EpisodeNumber,
    DateOnly? AirDate,
    decimal AverageRating);

public sealed record SearchPersonResult(
    Guid Id,
    string FullName,
    string? PhotoUrl,
    string? BirthPlace);
