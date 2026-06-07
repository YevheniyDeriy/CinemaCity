using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Series;

public static class SeriesEndpoints
{
    public static IEndpointRouteBuilder MapSeriesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/movies/{movieId:guid}/seasons", GetSeasonsAsync)
            .WithName("GetSeriesSeasons")
            .WithTags("Series");

        var admin = routes.MapGroup("/api/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin Series");

        admin.MapPost("/movies/{movieId:guid}/seasons", CreateSeasonAsync)
            .WithName("CreateSeason");

        admin.MapPut("/seasons/{seasonId:guid}", UpdateSeasonAsync)
            .WithName("UpdateSeason");

        admin.MapDelete("/seasons/{seasonId:guid}", DeleteSeasonAsync)
            .WithName("DeleteSeason");

        admin.MapPost("/seasons/{seasonId:guid}/episodes", CreateEpisodeAsync)
            .WithName("CreateEpisode");

        admin.MapPut("/episodes/{episodeId:guid}", UpdateEpisodeAsync)
            .WithName("UpdateEpisode");

        admin.MapDelete("/episodes/{episodeId:guid}", DeleteEpisodeAsync)
            .WithName("DeleteEpisode");

        return routes;
    }

    private static async Task<IResult> GetSeasonsAsync(Guid movieId, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var seriesExists = await dbContext.Movies
            .AsNoTracking()
            .AnyAsync(movie => movie.Id == movieId && !movie.IsDeleted && movie.ContentType == ContentType.Series, cancellationToken);

        if (!seriesExists)
        {
            return Results.NotFound(new { Message = "Series was not found." });
        }

        var seasons = await dbContext.Seasons
            .AsNoTracking()
            .Where(season => season.SeriesId == movieId)
            .OrderBy(season => season.SeasonNumber)
            .Select(season => new SeasonResponse(
                season.Id,
                season.SeriesId,
                season.SeasonNumber,
                season.Title,
                season.Description,
                season.ReleaseYear,
                season.Episodes
                    .OrderBy(episode => episode.EpisodeNumber)
                    .Select(episode => new EpisodeResponse(
                        episode.Id,
                        episode.SeasonId,
                        episode.EpisodeNumber,
                        episode.Title,
                        episode.AirDate,
                        episode.Duration,
                        episode.AverageRating,
                        episode.RatingsCount))
                    .ToArray()))
            .ToListAsync(cancellationToken);

        return Results.Ok(seasons);
    }

    private static async Task<IResult> CreateSeasonAsync(
        Guid movieId,
        SeasonUpsertRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var series = await dbContext.Movies
            .FirstOrDefaultAsync(movie => movie.Id == movieId && !movie.IsDeleted && movie.ContentType == ContentType.Series, cancellationToken);

        if (series is null)
        {
            return Results.NotFound(new { Message = "Series was not found." });
        }

        var validation = ValidateSeason(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var duplicate = await dbContext.Seasons
            .AnyAsync(season => season.SeriesId == movieId && season.SeasonNumber == request.SeasonNumber, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Season with this number already exists." });
        }

        var season = new Season
        {
            Id = Guid.NewGuid(),
            SeriesId = movieId,
            SeasonNumber = request.SeasonNumber,
            Title = TrimOrNull(request.Title),
            Description = TrimOrNull(request.Description),
            ReleaseYear = request.ReleaseYear
        };

        dbContext.Seasons.Add(season);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/movies/{movieId}/seasons", ToSeasonResponse(season));
    }

    private static async Task<IResult> UpdateSeasonAsync(
        Guid seasonId,
        SeasonUpsertRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateSeason(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var season = await dbContext.Seasons.FirstOrDefaultAsync(candidate => candidate.Id == seasonId, cancellationToken);

        if (season is null)
        {
            return Results.NotFound(new { Message = "Season was not found." });
        }

        var duplicate = await dbContext.Seasons
            .AnyAsync(candidate =>
                candidate.Id != seasonId
                && candidate.SeriesId == season.SeriesId
                && candidate.SeasonNumber == request.SeasonNumber,
                cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Season with this number already exists." });
        }

        season.SeasonNumber = request.SeasonNumber;
        season.Title = TrimOrNull(request.Title);
        season.Description = TrimOrNull(request.Description);
        season.ReleaseYear = request.ReleaseYear;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToSeasonResponse(season));
    }

    private static async Task<IResult> DeleteSeasonAsync(Guid seasonId, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var season = await dbContext.Seasons.FirstOrDefaultAsync(candidate => candidate.Id == seasonId, cancellationToken);

        if (season is null)
        {
            return Results.NotFound(new { Message = "Season was not found." });
        }

        dbContext.Seasons.Remove(season);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> CreateEpisodeAsync(
        Guid seasonId,
        EpisodeUpsertRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seasonExists = await dbContext.Seasons.AnyAsync(season => season.Id == seasonId, cancellationToken);

        if (!seasonExists)
        {
            return Results.NotFound(new { Message = "Season was not found." });
        }

        var validation = ValidateEpisode(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var duplicate = await dbContext.Episodes
            .AnyAsync(episode => episode.SeasonId == seasonId && episode.EpisodeNumber == request.EpisodeNumber, cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Episode with this number already exists." });
        }

        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = request.EpisodeNumber,
            Title = request.Title.Trim(),
            AirDate = request.AirDate,
            Duration = request.Duration
        };

        dbContext.Episodes.Add(episode);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/episodes/{episode.Id}", ToEpisodeResponse(episode));
    }

    private static async Task<IResult> UpdateEpisodeAsync(
        Guid episodeId,
        EpisodeUpsertRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateEpisode(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var episode = await dbContext.Episodes.FirstOrDefaultAsync(candidate => candidate.Id == episodeId, cancellationToken);

        if (episode is null)
        {
            return Results.NotFound(new { Message = "Episode was not found." });
        }

        var duplicate = await dbContext.Episodes
            .AnyAsync(candidate =>
                candidate.Id != episodeId
                && candidate.SeasonId == episode.SeasonId
                && candidate.EpisodeNumber == request.EpisodeNumber,
                cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { Message = "Episode with this number already exists." });
        }

        episode.EpisodeNumber = request.EpisodeNumber;
        episode.Title = request.Title.Trim();
        episode.AirDate = request.AirDate;
        episode.Duration = request.Duration;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToEpisodeResponse(episode));
    }

    private static async Task<IResult> DeleteEpisodeAsync(Guid episodeId, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var episode = await dbContext.Episodes.FirstOrDefaultAsync(candidate => candidate.Id == episodeId, cancellationToken);

        if (episode is null)
        {
            return Results.NotFound(new { Message = "Episode was not found." });
        }

        dbContext.Episodes.Remove(episode);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static string? ValidateSeason(SeasonUpsertRequest request)
    {
        if (request.SeasonNumber <= 0)
        {
            return "Season number must be greater than zero.";
        }

        if (request.ReleaseYear is < 1888 or > 2200)
        {
            return "Release year is invalid.";
        }

        return null;
    }

    private static string? ValidateEpisode(EpisodeUpsertRequest request)
    {
        if (request.EpisodeNumber <= 0)
        {
            return "Episode number must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 255)
        {
            return "Episode title is required and must be at most 255 characters.";
        }

        if (request.Duration is not null and <= 0)
        {
            return "Duration must be greater than zero.";
        }

        return null;
    }

    private static SeasonResponse ToSeasonResponse(Season season)
    {
        return new SeasonResponse(
            season.Id,
            season.SeriesId,
            season.SeasonNumber,
            season.Title,
            season.Description,
            season.ReleaseYear,
            []);
    }

    private static EpisodeResponse ToEpisodeResponse(Episode episode)
    {
        return new EpisodeResponse(
            episode.Id,
            episode.SeasonId,
            episode.EpisodeNumber,
            episode.Title,
            episode.AirDate,
            episode.Duration,
            episode.AverageRating,
            episode.RatingsCount);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record SeasonResponse(
    Guid Id,
    Guid SeriesId,
    int SeasonNumber,
    string? Title,
    string? Description,
    int? ReleaseYear,
    IReadOnlyCollection<EpisodeResponse> Episodes);

public sealed record EpisodeResponse(
    Guid Id,
    Guid SeasonId,
    int EpisodeNumber,
    string Title,
    DateOnly? AirDate,
    int? Duration,
    decimal AverageRating,
    int RatingsCount);

public sealed record SeasonUpsertRequest(
    int SeasonNumber,
    string? Title,
    string? Description,
    int? ReleaseYear);

public sealed record EpisodeUpsertRequest(
    int EpisodeNumber,
    string Title,
    DateOnly? AirDate,
    int? Duration);
