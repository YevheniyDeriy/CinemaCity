using CinemaCity.AuthService.Dtos;
using CinemaCity.AuthService.Services;
using CinemaCity.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaCity.AuthService.Controllers;

/// <summary>Account administration backed by the extracted user service and repositories.</summary>
[ApiController, Route("api/v1/users"), Tags("Users")]
public sealed class UsersController(IUserService service) : ControllerBase
{
    /// <summary>Read the current account; its ID comes exclusively from JWT claims.</summary>
    [HttpGet("me"), Authorize]
    [ProducesResponseType<UserDetailsDto>(200), ProducesResponseType(401)]
    public async Task<IActionResult> Me(CancellationToken ct) => new HttpResultAction(await service.MeAsync(User, ct));

    /// <summary>List CinemaCity users with pagination.</summary>
    [HttpGet, Authorize(Roles = "Admin")]
    [ProducesResponseType<UserPageDto>(200), ProducesResponseType<ValidationProblemDetails>(400), ProducesResponseType(401), ProducesResponseType(403)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => new HttpResultAction(await service.ListAsync(page, pageSize, ct));

    /// <summary>Replace an account's role set with User and/or Admin.</summary>
    [HttpPut("{id:guid}/roles"), Authorize(Roles = "Admin")]
    [ProducesResponseType(204), ProducesResponseType<ValidationProblemDetails>(400), ProducesResponseType(401), ProducesResponseType(403), ProducesResponseType(404)]
    public async Task<IActionResult> SetRoles(Guid id, UpdateRolesDto request, CancellationToken ct) => new HttpResultAction(await service.SetRolesAsync(id, new UpdateRolesRequest(request.Roles), ct));
}
