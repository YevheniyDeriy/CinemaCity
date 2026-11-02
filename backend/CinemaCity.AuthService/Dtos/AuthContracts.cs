using CinemaCity.AuthService.Models;

namespace CinemaCity.AuthService.Services;

/// <summary>RegisterRequest from the CinemaCity monolith.</summary>
public sealed record RegisterRequest(
    [property: System.ComponentModel.Description("CinemaCity username (3–30 characters).")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.StringLength(30, MinimumLength = 3)] string Username,
    [property: System.ComponentModel.Description("Unique account email address.")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.EmailAddress] string Email,
    [property: System.ComponentModel.Description("Password of at least 8 characters.")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.StringLength(256, MinimumLength = 8)] string Password,
    [property: System.ComponentModel.Description("Must match Password.")] [property: System.ComponentModel.DataAnnotations.Required] string ConfirmPassword);

/// <summary>LoginRequest from the CinemaCity monolith.</summary>
public sealed record LoginRequest(
    [property: System.ComponentModel.Description("Unique account email address.")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.EmailAddress] string Email,
    [property: System.ComponentModel.Description("Password of at least 8 characters.")] [property: System.ComponentModel.DataAnnotations.Required] [property: System.ComponentModel.DataAnnotations.StringLength(256)] string Password);

/// <summary>RefreshRequest from the CinemaCity monolith.</summary>
public sealed record RefreshRequest(
    [property: System.ComponentModel.Description("Refresh token; omit to use the HttpOnly session cookie.")] string? RefreshToken);

/// <summary>AuthUserResponse from the CinemaCity monolith.</summary>
public sealed record AuthUserResponse(
    [property: System.ComponentModel.Description("Id of the CinemaCity AuthUser.")] Guid Id,
    [property: System.ComponentModel.Description("CinemaCity username (3–30 characters).")] string Username,
    [property: System.ComponentModel.Description("Unique account email address.")] string Email,
    [property: System.ComponentModel.Description("Role of the CinemaCity AuthUser.")] UserRole Role,
    [property: System.ComponentModel.Description("AvatarUrl of the CinemaCity AuthUser.")] string? AvatarUrl);

/// <summary>AuthResponse from the CinemaCity monolith.</summary>
public sealed record AuthResponse(
    [property: System.ComponentModel.Description("AccessToken of the CinemaCity Auth.")] string AccessToken,
    [property: System.ComponentModel.Description("AccessTokenExpiresAt of the CinemaCity Auth.")] DateTimeOffset AccessTokenExpiresAt,
    [property: System.ComponentModel.Description("User of the CinemaCity Auth.")] AuthUserResponse User,
    [property: System.ComponentModel.Description("Refresh token; omit to use the HttpOnly session cookie.")] string RefreshToken,
    [property: System.ComponentModel.Description("RefreshTokenExpiresAt of the CinemaCity Auth.")] DateTimeOffset RefreshTokenExpiresAt);
