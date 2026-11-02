using System.ComponentModel;

namespace CinemaCity.CatalogService.Services;

/// <summary>Identity of the movie or series created by Catalog.</summary>
public sealed record CreatedMovieDto([property: Description("Generated stable UUID; use the Location header to read the resource.")] Guid Id);
