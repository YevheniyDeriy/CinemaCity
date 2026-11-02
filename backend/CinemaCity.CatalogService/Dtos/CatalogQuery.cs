using CinemaCity.CatalogService.Models;
namespace CinemaCity.CatalogService.Services;

/// <summary>Filters for the original CinemaCity movie catalog.</summary>
public sealed record CatalogQuery(
    int Page = 1, int PageSize = 20, string? Search = null, int? CategoryId = null,
    string SortBy = "rating", bool Desc = false, string? Genre = null, int? YearFrom = null,
    int? YearTo = null, decimal? RatingMin = null, string? ContentType = null)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Page < 1 || (long)(Page - 1) * PageSize > int.MaxValue) errors["page"] = ["Must be a positive page within the supported range."];
        if (PageSize is < 1 or > 100) errors["pageSize"] = ["Must be between 1 and 100."];
        if (CategoryId is <= 0) errors["categoryId"] = ["Must be a positive genre ID."];
        if (SortBy.ToLowerInvariant() is not ("title" or "year" or "rating" or "createdat" or "newest")) errors["sortBy"] = ["Use title, year, rating, createdAt or newest."];
        if (ContentType is not null && (!Enum.TryParse<ContentType>(ContentType, true, out var type) || !Enum.IsDefined(type))) errors["contentType"] = ["Use Movie or Series."];
        if (YearFrom > YearTo) errors["yearFrom"] = ["Cannot be greater than yearTo."];
        if (RatingMin is < 0 or > 10) errors["ratingMin"] = ["Must be between 0 and 10."];
        return errors;
    }
}
