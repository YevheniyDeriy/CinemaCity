using CinemaCity.CatalogService.Dtos;
using CinemaCity.CatalogService.Services;
using CinemaCity.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaCity.CatalogService.Controllers;

/// <summary>Thin versioned API for the movie/series catalog extracted from CinemaCity.MovieEndpoints.</summary>
[ApiController, Route("api/v1/catalog"), Tags("Catalog")]
public sealed class CatalogController(ICatalogService service) : ControllerBase
{
    /// <summary>Search, filter by genre, sort and paginate the existing CinemaCity movies and series.</summary>
    [HttpGet("items"), AllowAnonymous]
    [ProducesResponseType<PagedResult<MovieCardResponse>>(200), ProducesResponseType<ValidationProblemDetails>(400)]
    public async Task<IActionResult> List([FromQuery] CatalogQuery query, CancellationToken ct) => new HttpResultAction(await service.GetPageAsync(query, ct));

    /// <summary>Read full movie or series details, genres, countries, languages and cast.</summary>
    [HttpGet("items/{id:guid}"), AllowAnonymous]
    [ProducesResponseType<MovieDetailsResponse>(200), ProducesResponseType<ProblemDetails>(404)]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => new HttpResultAction(await service.GetMovieAsync(id, ct));

    /// <summary>Create a CinemaCity movie or series; only Admin may change the catalog.</summary>
    [HttpPost("items"), Authorize(Roles = "Admin")]
    [ProducesResponseType<CreatedMovieDto>(201), ProducesResponseType<ValidationProblemDetails>(400), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Create(CreateMovieDto request, CancellationToken ct) => new HttpResultAction(await service.CreateMovieAsync(request.ToRequest(), ct));

    /// <summary>Replace the editable fields of an existing movie or series.</summary>
    [HttpPut("items/{id:guid}"), Authorize(Roles = "Admin")]
    [ProducesResponseType(204), ProducesResponseType<ValidationProblemDetails>(400), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404), ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Update(Guid id, UpdateMovieDto request, CancellationToken ct) => new HttpResultAction(await service.UpdateMovieAsync(id, request.ToRequest(), ct));

    /// <summary>Soft-delete a movie or series while preserving its stable UUID.</summary>
    [HttpDelete("items/{id:guid}"), Authorize(Roles = "Admin")]
    [ProducesResponseType(204), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => new HttpResultAction(await service.DeleteMovieAsync(id, ct));

    /// <summary>Read the original CinemaCity genres used as catalog categories.</summary>
    [HttpGet("categories"), AllowAnonymous]
    [ProducesResponseType<GenreResponse[]>(200)]
    public async Task<IActionResult> Categories(CancellationToken ct) => new HttpResultAction(await service.GetGenresAsync(ct));
}
