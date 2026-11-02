using System.Security.Claims;
using CinemaCity.AuthService.Services;
using CinemaCity.ServiceDefaults;

namespace CinemaCity.AuthService.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/internal/users/{id:guid}", (Guid id, IUserService service, HttpContext context, IConfiguration config, CancellationToken ct) =>
            global::CinemaCity.ServiceDefaults.ServiceDefaults.IsInternalRequest(context, config) ? service.SnapshotAsync(id, ct) : Task.FromResult(Results.Unauthorized() as IResult)).ExcludeFromDescription();
    }
}
