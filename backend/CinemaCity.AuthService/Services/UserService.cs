using System.Security.Claims;
using System.Text.Json;
using CinemaCity.AuthService.Repositories;
using CinemaCity.AuthService.Models;
using CinemaCity.AuthService.Dtos;

namespace CinemaCity.AuthService.Services;

/// <summary>Account administration and public identity snapshots from the extracted user model.</summary>
public interface IUserService
{
    Task<IResult> MeAsync(ClaimsPrincipal principal, CancellationToken ct);
    Task<IResult> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<IResult> SetRolesAsync(Guid id, UpdateRolesRequest request, CancellationToken ct);
    Task<IResult> SnapshotAsync(Guid id, CancellationToken ct);
}

public sealed class UserService(IUserRepository repository, IUnitOfWork unitOfWork, ILogger<UserService> logger) : IUserService
{
    public async Task<IResult> MeAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Results.Unauthorized();
        var user = await repository.GetByIdAsync(id, ct);
        return user is null || !user.IsActive ? Results.Unauthorized() : Results.Ok(ToDto(user));
    }

    public async Task<IResult> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["page"] = ["page must be positive and pageSize must be between 1 and 100."] });
        var (users, totalCount) = await repository.GetPageAsync(page, pageSize, ct);
        return Results.Ok(new UserPageDto(users.Select(ToDto).ToArray(), page, pageSize, totalCount));
    }

    public async Task<IResult> SetRolesAsync(Guid id, UpdateRolesRequest request, CancellationToken ct)
    {
        var names = request.Roles?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (names.Length == 0 || names.Any(n => n is not ("User" or "Admin")))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["Supply one or more roles: User, Admin."] });
        var user = await repository.GetByIdAsync(id, ct);
        if (user is null) return Results.NotFound();
        repository.ReplaceRoles(user, names);
        user.Role = names.Contains("Admin") ? UserRole.Admin : UserRole.User;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Roles changed for user {UserId}", id);
        return Results.NoContent();
    }

    public async Task<IResult> SnapshotAsync(Guid id, CancellationToken ct)
    {
        var user = await repository.GetByIdAsync(id, ct);
        return user is null ? Results.NotFound() : Results.Ok(new { user.Id, user.Username, user.Email, user.AvatarUrl, user.Role, user.IsActive, user.CreatedAt, user.UpdatedAt });
    }

    private static UserDetailsDto ToDto(User user) => new(user.Id, user.Username, user.Email, user.AvatarUrl, user.Role.ToString(), user.Roles.Select(r => r.Role.Name).ToArray(), user.IsActive, user.CreatedAt);
}

/// <summary>The complete replacement set of account roles.</summary>
public sealed record UpdateRolesRequest([property: System.ComponentModel.Description("Role names: User and/or Admin.")] string[] Roles);
/// <summary>Account information; password and refresh-token hashes never leave Auth.</summary>
public sealed record UserDetailsDto(
    [property: System.ComponentModel.Description("Stable UUID retained from the original CinemaCity account.")] Guid Id,
    [property: System.ComponentModel.Description("Original CinemaCity username.")] string Username,
    [property: System.ComponentModel.Description("Account email.")] string Email,
    [property: System.ComponentModel.Description("Optional avatar URL.")] string? AvatarUrl,
    [property: System.ComponentModel.Description("Primary role retained for the existing UI.")] string Role,
    [property: System.ComponentModel.Description("Complete set of role names.")] IReadOnlyCollection<string> Roles,
    [property: System.ComponentModel.Description("Whether sign-in and refresh are allowed.")] bool IsActive,
    [property: System.ComponentModel.Description("Account creation timestamp in UTC.")] DateTimeOffset CreatedAt);
