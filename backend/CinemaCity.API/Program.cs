using CinemaCity.API.Auth;
using CinemaCity.Application;
using CinemaCity.Infrastructure;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCinemaCityAuth(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
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

app.MapGet("/api/movies", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var movies = await dbContext.Movies
        .AsNoTracking()
        .Where(movie => !movie.IsDeleted)
        .OrderByDescending(movie => movie.AverageRating)
        .ThenByDescending(movie => movie.RatingsCount)
        .Select(movie => new
        {
            movie.Id,
            movie.Title,
            movie.OriginalTitle,
            movie.ContentType,
            movie.ReleaseYear,
            movie.PosterUrl,
            movie.AverageRating,
            movie.RatingsCount
        })
        .Take(20)
        .ToListAsync(cancellationToken);

    return Results.Ok(movies);
})
.WithName("GetMovies");

app.MapAuthEndpoints();

app.Run();
