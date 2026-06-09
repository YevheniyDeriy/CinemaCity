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
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCinemaCityAuth(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174", "http://127.0.0.1:5174")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CinemaCity API",
        Version = "v1",
        Description = "API для каталогу, автентифікації та керування CinemaCity."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Вставте JWT access token з відповіді login без префікса Bearer."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    await EnsureSampleEpisodesAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    var seedLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentDataSeeder");
    await DevelopmentDataSeeder.SeedAsync(app.Services, seedLogger);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CinemaCity API v1");
        options.DocumentTitle = "CinemaCity API";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    Application = "CinemaCity API",
    Version = "1.0"
}));

app.MapGet("/health/database", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { Database = "PostgreSQL", Status = "Available" })
        : Results.Problem("PostgreSQL is unavailable");
})
.WithName("DatabaseHealth");

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapHomeEndpoints();
app.MapMovieEndpoints();
app.MapSeriesEndpoints();
app.MapRatingEndpoints();
app.MapReviewEndpoints();
app.MapProfileEndpoints();
app.MapSearchEndpoints();

app.Run();

static async Task EnsureSampleEpisodesAsync(AppDbContext dbContext)
{
    if (await dbContext.Seasons.AnyAsync())
    {
        return;
    }

    var series = await dbContext.Movies
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

    dbContext.Seasons.Add(season);
    dbContext.Episodes.AddRange(episodes);
    await dbContext.SaveChangesAsync();
}
