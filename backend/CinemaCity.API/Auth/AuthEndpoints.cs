using System.Security.Claims;
using CinemaCity.Domain.Entities;
using CinemaCity.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register");

        group.MapPost("/confirm-email", ConfirmEmailAsync)
            .WithName("ConfirmEmail");

        return group;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AppDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        TokenService tokenService,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = NormalizeEmail(request.Email);

        if (username.Length is < 3 or > 30)
        {
            return Results.BadRequest(new { Message = "Username must be between 3 and 30 characters." });
        }

        if (!email.Contains('@', StringComparison.Ordinal) || email.Length > 255)
        {
            return Results.BadRequest(new { Message = "Email format is invalid." });
        }

        if (request.Password.Length < 8)
        {
            return Results.BadRequest(new { Message = "Password must contain at least 8 characters." });
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { Message = "Password confirmation does not match." });
        }

        var usernameExists = await dbContext.Users
            .AnyAsync(user => user.Username == username, cancellationToken);

        if (usernameExists)
        {
            return Results.Conflict(new { Message = "Username is already taken." });
        }

        var emailExists = await dbContext.Users
            .AnyAsync(user => user.Email == email, cancellationToken);

        if (emailExists)
        {
            return Results.Conflict(new { Message = "Email is already registered." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            IsEmailConfirmed = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var (confirmationToken, _) = tokenService.CreateEmailConfirmationToken(user);

        return Results.Created($"/api/users/{user.Id}", new RegisterResponse(
            user.Id,
            user.Username,
            user.Email,
            user.IsEmailConfirmed,
            confirmationToken));
    }

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        AppDbContext dbContext,
        TokenService tokenService,
        CancellationToken cancellationToken)
    {
        var principal = tokenService.ValidateToken(request.Token);

        if (principal is null || principal.FindFirstValue("purpose") != "email-confirmation")
        {
            return Results.BadRequest(new { Message = "Email confirmation token is invalid or expired." });
        }

        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Results.BadRequest(new { Message = "Email confirmation token is invalid." });
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return Results.NotFound(new { Message = "User was not found." });
        }

        user.IsEmailConfirmed = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { Message = "Email confirmed." });
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
