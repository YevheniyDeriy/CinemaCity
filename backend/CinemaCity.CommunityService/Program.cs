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

LocalDevelopmentEnvironment.Load("CinemaCity.CommunityService");
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCinemaCityAuth(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddServiceDefaults(builder.Configuration, "CinemaCity Community");
builder.Services.AddCommunityIntegration(builder.Configuration);
builder.Services.AddSwaggerGen(options => options.OperationFilter<OperationReplayDocumentation>());

builder.Configuration["Services:Enabled"] = "true";
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

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<CinemaCity.Infrastructure.Data.AppDbContext>().Database.ExecuteSqlRawAsync(IdempotencyMiddleware.SchemaSql);

// Normalize versioned community URLs before endpoint selection. Legacy URLs remain compatibility aliases.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1", out var remainder)) context.Request.Path = "/api" + remainder;
    await next(context);
});
app.UseRouting();
app.UseServiceDefaults(normalizeErrors: true);
app.UseStaticFiles();
if (app.Configuration.GetValue("Services:Enabled", false)) app.UseMiddleware<ReferenceProjectionMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapDatabaseHealth<CinemaCity.Infrastructure.Data.AppDbContext>("CinemaCity.Community");
app.MapCommunityInternalEndpoints();
app.MapGet("/health/ready", async (CinemaCity.Infrastructure.Data.AppDbContext db, IHttpClientFactory clients, CancellationToken ct) =>
{
    if (!await db.Database.CanConnectAsync(ct)) return Results.Problem("Community database is unavailable", statusCode: 503);
    try
    {
        using var response = await clients.CreateClient("CatalogOwner").GetAsync("/health", ct);
        return response.IsSuccessStatusCode ? Results.Ok(new { status = "Ready" }) : Results.Problem("Catalog is unavailable", statusCode: 503);
    }
    catch (HttpRequestException) { return Results.Problem("Catalog is unavailable", statusCode: 503); }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Results.Problem("Catalog timed out", statusCode: 503); }
}).AllowAnonymous().WithSummary("Community database and Catalog readiness").Produces(200).ProducesProblem(503);

app.MapGet("/", () => Results.Ok(new
{
    Application = "CinemaCity Community",
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
        Title = "РџРµСЂС€РёР№ СЃРµР·РѕРЅ",
        Description = "РўРµСЃС‚РѕРІРёР№ СЃРµР·РѕРЅ РґР»СЏ РїРµСЂРµРІС–СЂРєРё СЂРµР№С‚РёРЅРіРѕРІРѕС— С‚Р°Р±Р»РёС†С– РµРїС–Р·РѕРґС–РІ.",
        ReleaseYear = series.ReleaseYear
    };

    var episodes = new[]
    {
        new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            EpisodeNumber = 1,
            Title = "РџРѕС‡Р°С‚РѕРє",
            Description = "Р“РµСЂРѕС— РІС…РѕРґСЏС‚СЊ Сѓ РєРѕРЅС„Р»С–РєС‚, СЏРєРёР№ Р·Р°РґР°С” С‚РѕРЅ СѓСЃСЊРѕРјСѓ СЃРµР·РѕРЅСѓ.",
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
            Title = "РќР°СЃР»С–РґРєРё",
            Description = "РџРµСЂС€С– СЂС–С€РµРЅРЅСЏ РїРѕРІРµСЂС‚Р°СЋС‚СЊСЃСЏ РЅРµСЃРїРѕРґС–РІР°РЅРёРјРё РЅР°СЃР»С–РґРєР°РјРё.",
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
            Title = "РўРѕС‡РєР° РєРёРїС–РЅРЅСЏ",
            Description = "РќР°РїСЂСѓРіР° СЂРѕСЃС‚Рµ, Р° РґРѕРІС–СЂР° РјС–Р¶ РїРµСЂСЃРѕРЅР°Р¶Р°РјРё РїРѕС‡РёРЅР°С” Р»Р°РјР°С‚РёСЃСЏ.",
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
            Title = "Р‘РµР· РїРѕРІРµСЂРЅРµРЅРЅСЏ",
            Description = "РЎРµР·РѕРЅ РїРµСЂРµС…РѕРґРёС‚СЊ Сѓ С„Р°Р·Сѓ, РґРµ РєРѕР¶РµРЅ РІРёР±С–СЂ РјР°С” С†С–РЅСѓ.",
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
