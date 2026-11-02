using CinemaCity.Domain.Enums;

namespace CinemaCity.API.Auth;

public sealed record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string ConfirmPassword);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record AuthUserResponse(
    Guid Id,
    string Username,
    string Email,
    UserRole Role,
    string? AvatarUrl);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    AuthUserResponse User);
