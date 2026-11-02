using CinemaCity.CatalogService.Data;
using CinemaCity.CatalogService.Models;
using CinemaCity.CatalogService.Services;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.CatalogService.Repositories;

public interface ICatalogRepository
{
    Task<PagedResult<MovieCardResponse>> GetPageAsync(CatalogQuery query, CancellationToken ct);
}

/// <summary>Executes all catalog filtering, ordering and pagination in PostgreSQL.</summary>
public sealed class CatalogRepository(CatalogDbContext db, ILogger<CatalogRepository> logger) : ICatalogRepository
{
    public async Task<PagedResult<MovieCardResponse>> GetPageAsync(CatalogQuery filter, CancellationToken ct)
    {
        var query = db.Movies.AsNoTracking().Where(m => !m.IsDeleted);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(m => m.Title.ToLower().Contains(term) || (m.OriginalTitle != null && m.OriginalTitle.ToLower().Contains(term)));
        }
        if (filter.CategoryId is int category) query = query.Where(m => m.Genres.Any(g => g.Id == category));
        if (!string.IsNullOrWhiteSpace(filter.Genre)) query = query.Where(m => m.Genres.Any(g => g.Slug == filter.Genre || g.Name == filter.Genre));
        if (filter.YearFrom is int from) query = query.Where(m => m.ReleaseYear >= from);
        if (filter.YearTo is int to) query = query.Where(m => m.ReleaseYear <= to);
        if (filter.RatingMin is decimal rating) query = query.Where(m => m.AverageRating >= rating);
        if (!string.IsNullOrWhiteSpace(filter.ContentType))
        {
            var type = Enum.Parse<ContentType>(filter.ContentType, true);
            query = query.Where(m => m.ContentType == type);
        }
        var total = await query.CountAsync(ct);
        var ordered = (filter.SortBy.ToLowerInvariant(), filter.Desc) switch
        {
            ("title", false) => query.OrderBy(m => m.Title), ("title", true) => query.OrderByDescending(m => m.Title),
            ("year", false) => query.OrderBy(m => m.ReleaseYear), ("year", true) => query.OrderByDescending(m => m.ReleaseYear),
            ("newest" or "createdat", false) => query.OrderBy(m => m.CreatedAt), ("newest" or "createdat", true) => query.OrderByDescending(m => m.CreatedAt),
            (_, false) => query.OrderBy(m => m.AverageRating), _ => query.OrderByDescending(m => m.AverageRating)
        };
        var page = ordered.ThenBy(m => m.Id).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(m => new MovieCardResponse(m.Id, m.Title, m.OriginalTitle, m.ContentType, m.ReleaseYear, m.PosterUrl, m.BackdropUrl, m.AverageRating, m.RatingsCount,
                m.Genres.OrderBy(g => g.Name).Select(g => new GenreResponse(g.Id, g.Name, g.Slug)).ToArray()));
        logger.LogDebug("Catalog page SQL: {Sql}", page.ToQueryString());
        return new PagedResult<MovieCardResponse>(await page.ToArrayAsync(ct), filter.Page, filter.PageSize, total);
    }
}

