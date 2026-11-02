using System.Text;
using System.Text.RegularExpressions;
using CinemaCity.ServiceDefaults;
using CinemaCity.CatalogService.Services;
namespace CinemaCity.CatalogService.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/v1/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin");

        admin.MapGet("/genres", (ICatalogAdminService service , CancellationToken cancellationToken) => service.GetGenresAsync(cancellationToken))
            .WithName("AdminGetGenres").WithSummary("Admin Get Genres").Produces(200).Produces(400).Produces(404);

        admin.MapPost("/genres", (ICatalogAdminService service , GenreUpsertRequest request, CancellationToken cancellationToken) => service.CreateGenreAsync(request, cancellationToken))
            .WithName("AdminCreateGenre").WithSummary("Admin Create Genre").Produces(201).Produces(400).Produces(409);

        admin.MapPut("/genres/{id:int}", (ICatalogAdminService service , int id, GenreUpsertRequest request, CancellationToken cancellationToken) => service.UpdateGenreAsync(id, request, cancellationToken))
            .WithName("AdminUpdateGenre").WithSummary("Admin Update Genre").Produces(204).Produces(400).Produces(404);

        admin.MapDelete("/genres/{id:int}", (ICatalogAdminService service , int id, CancellationToken cancellationToken) => service.DeleteGenreAsync(id, cancellationToken))
            .WithName("AdminDeleteGenre").WithSummary("Admin Delete Genre").Produces(204).Produces(400).Produces(404);

        admin.MapGet("/persons", (ICatalogAdminService service , string? q, CancellationToken cancellationToken) => service.GetPersonsAsync(q, cancellationToken))
            .WithName("AdminGetPersons").WithSummary("Admin Get Persons").Produces(200).Produces(400).Produces(404);

        admin.MapPost("/persons", (ICatalogAdminService service , PersonUpsertRequest request, CancellationToken cancellationToken) => service.CreatePersonAsync(request, cancellationToken))
            .WithName("AdminCreatePerson").WithSummary("Admin Create Person").Produces(201).Produces(400).Produces(409);

        admin.MapPut("/persons/{id:guid}", (ICatalogAdminService service , Guid id, PersonUpsertRequest request, CancellationToken cancellationToken) => service.UpdatePersonAsync(id, request, cancellationToken))
            .WithName("AdminUpdatePerson").WithSummary("Admin Update Person").Produces(204).Produces(400).Produces(404);

        admin.MapDelete("/persons/{id:guid}", (ICatalogAdminService service , Guid id, CancellationToken cancellationToken) => service.DeletePersonAsync(id, cancellationToken))
            .WithName("AdminDeletePerson").WithSummary("Admin Delete Person").Produces(204).Produces(400).Produces(404);

        admin.MapPost("/movies/{movieId:guid}/poster", (ICatalogAdminService service , Guid movieId, HttpRequest request, CancellationToken cancellationToken) => service.UploadMoviePosterAsync(movieId, request, cancellationToken))
            .DisableAntiforgery()
            .WithName("AdminUploadMoviePoster").WithSummary("Admin Upload Movie Poster").Produces(200).Produces(400).Produces(401);

        admin.MapPost("/persons/{personId:guid}/photo", (ICatalogAdminService service , Guid personId, HttpRequest request, CancellationToken cancellationToken) => service.UploadPersonPhotoAsync(personId, request, cancellationToken))
            .DisableAntiforgery()
            .WithName("AdminUploadPersonPhoto").WithSummary("Admin Upload Person Photo").Produces(200).Produces(400).Produces(401);

        admin.MapPost("/uploads/poster", (ICatalogAdminService service, HttpRequest request, CancellationToken cancellationToken) =>
                service.UploadImageAsync(request, "posters", cancellationToken))
            .DisableAntiforgery()
            .WithName("AdminUploadPoster").WithSummary("Admin Upload Poster").Produces(200).Produces(400).Produces(401);

        admin.MapPost("/uploads/photo", (ICatalogAdminService service, HttpRequest request, CancellationToken cancellationToken) =>
                service.UploadImageAsync(request, "photos", cancellationToken))
            .DisableAntiforgery()
            .WithName("AdminUploadPhoto").WithSummary("Admin Upload Photo").Produces(200).Produces(400).Produces(401);

        ImportEndpoints.MapTmdbImportEndpoints(admin);

        return routes;
    }
}
