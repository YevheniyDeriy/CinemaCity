using CinemaCity.API.Integration;
using CinemaCity.ServiceDefaults;
using CinemaCity.API;
using CinemaCity.API.Admin;
using CinemaCity.API.Auth;
using CinemaCity.API.Home;
using CinemaCity.API.Movies;
using CinemaCity.API.Profile;
using CinemaCity.API.Ratings;
using CinemaCity.API.Reviews;
using CinemaCity.API.Search;
using CinemaCity.API.Series;
using CinemaCity.Application;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;

LocalDevelopmentEnvironment.Load("CinemaCity.API");
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCinemaCityAuth(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddServiceDefaults(builder.Configuration, "CinemaCity API — моноліт");
builder.Services.AddCommunityIntegration(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true)) using (var scope = app.Services.CreateScope())
{
    var databaseManager = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();
    await databaseManager.MigrateAsync();
    if (!app.Configuration.GetValue("Services:Enabled", false)) await EnsureSampleEpisodesAsync(
        scope.ServiceProvider.GetRequiredService<IRepository<Movie>>(),
        scope.ServiceProvider.GetRequiredService<IRepository<Season>>(),
        scope.ServiceProvider.GetRequiredService<IRepository<Episode>>(),
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
}

// Normalize versioned community URLs before endpoint selection. Legacy URLs remain compatibility aliases.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1", out var remainder)) context.Request.Path = "/api" + remainder;
    await next(context);
});
app.UseRouting();
app.UseServiceDefaults();
app.UseStaticFiles();
if (app.Configuration.GetValue("Services:Enabled", false)) app.UseMiddleware<ReferenceProjectionMiddleware>();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapDatabaseHealth<CinemaCity.Infrastructure.Data.AppDbContext>("CinemaCity.Monolith");
app.MapCommunityInternalEndpoints();

app.MapGet("/", () => Results.Ok(new
{
    Application = "CinemaCity API",
    Version = "1.0"
}));

app.MapGet("/health/database", async (IDatabaseManager databaseManager, CancellationToken cancellationToken) =>
{
    var canConnect = await databaseManager.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { Database = "PostgreSQL", Status = "Available" })
        : Results.Problem("PostgreSQL is unavailable", statusCode: 503);
})
.WithName("DatabaseHealth");

if (!app.Configuration.GetValue("Services:Enabled", false))
{
    app.MapAuthEndpoints();
    app.MapAdminEndpoints();
    app.MapMovieEndpoints();
    app.MapSeriesEndpoints();
    app.MapSearchEndpoints();
}
app.MapHomeEndpoints();
app.MapRatingEndpoints();
app.MapReviewEndpoints();
app.MapProfileEndpoints();

app.Run();

static async Task EnsureSampleEpisodesAsync(
    IRepository<Movie> movieRepository,
    IRepository<Season> seasonRepository,
    IRepository<Episode> episodeRepository,
    IUnitOfWork unitOfWork)
{
    if (await seasonRepository.Query().AnyAsync())
    {
        return;
    }

    var series = await movieRepository.Query()
        .Where(movie => !movie.IsDeleted && movie.ContentType == ContentType.Series)
        .OrderByDescending(movie => movie.AverageRating)
        .FirstOrDefaultAsync();

    if (series is null)
    {
        return;
    }

    var season = new Season
    {
        Id = Guid.NewGuid(),
        SeriesId = series.Id,
        SeasonNumber = 1,
        Title = "Перший сезон",
        Description = "Тестовий сезон для перевірки рейтингової таблиці епізодів.",
        ReleaseYear = series.ReleaseYear
    };

    var episodes = new[]
    {
        new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            EpisodeNumber = 1,
            Title = "Початок",
            Description = "Герої входять у конфлікт, який задає тон усьому сезону.",
            AirDate = new DateOnly(series.ReleaseYear, 1, 20),
            Duration = 48,
            AverageRating = 8.4m,
            RatingsCount = 420
        },
        new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            EpisodeNumber = 2,
            Title = "Наслідки",
            Description = "Перші рішення повертаються несподіваними наслідками.",
            AirDate = new DateOnly(series.ReleaseYear, 1, 27),
            Duration = 46,
            AverageRating = 8.7m,
            RatingsCount = 515
        },
        new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            EpisodeNumber = 3,
            Title = "Точка кипіння",
            Description = "Напруга росте, а довіра між персонажами починає ламатися.",
            AirDate = new DateOnly(series.ReleaseYear, 2, 3),
            Duration = 50,
            AverageRating = 9.1m,
            RatingsCount = 610
        },
        new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            EpisodeNumber = 4,
            Title = "Без повернення",
            Description = "Сезон переходить у фазу, де кожен вибір має ціну.",
            AirDate = new DateOnly(series.ReleaseYear, 2, 10),
            Duration = 49,
            AverageRating = 8.2m,
            RatingsCount = 388
        }
    };

    seasonRepository.Add(season);
    episodeRepository.AddRange(episodes);
    await unitOfWork.SaveChangesAsync();
}

public partial class Program;
