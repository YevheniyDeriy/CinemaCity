using CinemaCity.CatalogService.Data;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.CatalogService.Services;

public static class CatalogSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (!configuration.GetValue("Seed:Enabled", false)) return;
        var db = services.GetRequiredService<CatalogDbContext>();
        if (await db.Movies.AnyAsync()) return;
        // Explicit opt-in for empty development databases only. Existing CinemaCity data is imported separately.
        var genre = await db.Genres.FirstOrDefaultAsync(g => g.Slug == "drama");
        if (genre is null) { genre = new Genre { Name = "Драма", Slug = "drama" }; db.Genres.Add(genre); }
        db.Movies.AddRange(
            new Movie { Id = Guid.NewGuid(), Title = "CinemaCity — тестовий фільм", ContentType = ContentType.Movie, ReleaseYear = 2026, Description = "Лише для перевірки порожнього середовища. Існуючі фільми переносить import-monolith.", Genres = [genre] },
            new Movie { Id = Guid.NewGuid(), Title = "CinemaCity — тестовий серіал", ContentType = ContentType.Series, ReleaseYear = 2026, Genres = [genre], Seasons = [new Season { Id = Guid.NewGuid(), SeasonNumber = 1, Title = "Перший сезон", Episodes = [new Episode { Id = Guid.NewGuid(), EpisodeNumber = 1, Title = "Пілот" }] }] });
        await db.SaveChangesAsync();
    }
}
