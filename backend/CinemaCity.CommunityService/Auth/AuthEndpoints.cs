using System.Security.Claims;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CinemaCity.API.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register");

        group.MapPost("/login", LoginAsync)
            .WithName("Login");

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshToken");

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout");

        group.MapGet("/me", Me)
            .RequireAuthorization()
            .WithName("CurrentUser");

        group.MapGet("/google", GoogleLogin)
            .WithName("GoogleLogin");

        group.MapGet("/google/callback", GoogleCallbackAsync)
            .WithName("GoogleCallback");

        return group;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext httpContext,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        TokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
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

        var usernameExists = await userRepository.UsernameExistsAsync(username, cancellationToken);

        if (usernameExists)
        {
            return Results.Conflict(new { Message = "Username is already taken." });
        }

        var emailExists = await userRepository.EmailExistsAsync(email, cancellationToken);

        if (emailExists)
        {
            return Results.Conflict(new { Message = "Email is already registered." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        userRepository.Add(user);

        var authResponse = CreateAuthSession(
            user,
            httpContext,
            refreshTokenRepository,
            tokenService,
            jwtOptions.Value);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/users/{user.Id}", authResponse);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        TokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || user.PasswordHash is null)
        {
            return Results.Unauthorized();
        }

        if (user.LockoutEndAt is not null && user.LockoutEndAt > DateTimeOffset.UtcNow)
        {
            return Results.Problem("Account is temporarily locked.", statusCode: StatusCodes.Status423Locked);
        }

        var passwordResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginAttempts += 1;

            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEndAt = DateTimeOffset.UtcNow.AddMinutes(15);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Results.Unauthorized();
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var authResponse = CreateAuthSession(
            user,
            httpContext,
            refreshTokenRepository,
            tokenService,
            jwtOptions.Value);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Ok(authResponse);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest? request,
        HttpContext httpContext,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        TokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var refreshToken = request?.RefreshToken
            ?? httpContext.Request.Cookies[AuthSetup.RefreshTokenCookieName];

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Results.Unauthorized();
        }

        var tokenHash = TokenService.HashRefreshToken(refreshToken);
        var storedToken = await refreshTokenRepository.GetByTokenHashWithUserAsync(tokenHash, cancellationToken);

        if (storedToken is null || storedToken.RevokedAt is not null || storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Results.Unauthorized();
        }

        storedToken.RevokedAt = DateTimeOffset.UtcNow;

        var authResponse = CreateAuthSession(
            storedToken.User,
            httpContext,
            refreshTokenRepository,
            tokenService,
            jwtOptions.Value);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Ok(authResponse);
    }

    private static async Task<IResult> LogoutAsync(
        RefreshRequest? request,
        HttpContext httpContext,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var refreshToken = request?.RefreshToken
            ?? httpContext.Request.Cookies[AuthSetup.RefreshTokenCookieName];

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var tokenHash = TokenService.HashRefreshToken(refreshToken);
            var storedToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

            if (storedToken is not null && storedToken.RevokedAt is null)
            {
                storedToken.RevokedAt = DateTimeOffset.UtcNow;
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        httpContext.Response.Cookies.Delete(AuthSetup.RefreshTokenCookieName, CreateRefreshTokenCookieOptions(httpContext, null));
        httpContext.Response.Cookies.Delete(AuthSetup.RefreshTokenCookieName, CreateLegacyRefreshTokenCookieOptions(httpContext));

        return Results.Ok(new { Message = "Logged out." });
    }

    private static Ok<AuthUserResponse> Me(ClaimsPrincipal principal)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        return TypedResults.Ok(new AuthUserResponse(
            userId,
            principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Enum.Parse<UserRole>(principal.FindFirstValue(ClaimTypes.Role) ?? "User"),
            AvatarUrl: null));
    }

    private static ChallengeHttpResult GoogleLogin(HttpContext httpContext, string? returnUrl = null)
    {
        var redirectUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? "/api/auth/google/callback"
            : $"/api/auth/google/callback?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return TypedResults.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> GoogleCallbackAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        TokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var externalAuth = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!externalAuth.Succeeded || externalAuth.Principal is null)
        {
            return Results.BadRequest(new { Message = "Google authentication failed." });
        }

        var googleId = externalAuth.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = externalAuth.Principal.FindFirstValue(ClaimTypes.Email);
        var name = externalAuth.Principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(googleId) || string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new { Message = "Google profile does not contain required identity data." });
        }

        var normalizedEmail = NormalizeEmail(email);
        var user = await userRepository.GetByGoogleIdOrEmailAsync(googleId, normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Username = await CreateUniqueUsernameAsync(userRepository, name, normalizedEmail, cancellationToken),
                Email = normalizedEmail,
                GoogleId = googleId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            userRepository.Add(user);
        }
        else
        {
            user.GoogleId ??= googleId;
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var authResponse = CreateAuthSession(
            user,
            httpContext,
            refreshTokenRepository,
            tokenService,
            jwtOptions.Value);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            return Results.Redirect(returnUrl);
        }

        return Results.Ok(authResponse);
    }

    private static AuthResponse CreateAuthSession(
        User user,
        HttpContext httpContext,
        IRefreshTokenRepository refreshTokenRepository,
        TokenService tokenService,
        JwtOptions jwtOptions)
    {
        var (accessToken, accessTokenExpiresAt) = tokenService.CreateAccessToken(user);
        var refreshToken = TokenService.CreateRefreshToken();
        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.RefreshTokenDays);

        refreshTokenRepository.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenService.HashRefreshToken(refreshToken),
            ExpiresAt = refreshTokenExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        });

        AppendRefreshTokenCookie(httpContext, refreshToken, refreshTokenExpiresAt);

        return new AuthResponse(
            accessToken,
            accessTokenExpiresAt,
            ToUserResponse(user));
    }

    private static void AppendRefreshTokenCookie(
        HttpContext httpContext,
        string refreshToken,
        DateTimeOffset expiresAt)
    {
        httpContext.Response.Cookies.Append(
            AuthSetup.RefreshTokenCookieName,
            refreshToken,
            CreateRefreshTokenCookieOptions(httpContext, expiresAt));
    }

    private static CookieOptions CreateRefreshTokenCookieOptions(HttpContext httpContext, DateTimeOffset? expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            Expires = expiresAt
        };
    }

    private static CookieOptions CreateLegacyRefreshTokenCookieOptions(HttpContext httpContext)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        };
    }

    private static AuthUserResponse ToUserResponse(User user)
    {
        return new AuthUserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.AvatarUrl);
    }

    private static async Task<string> CreateUniqueUsernameAsync(
        IUserRepository userRepository,
        string? name,
        string email,
        CancellationToken cancellationToken)
    {
        var baseUsername = SlugifyUsername(name)
            ?? SlugifyUsername(email.Split('@')[0])
            ?? $"user{Random.Shared.Next(1000, 9999)}";

        var username = baseUsername;
        var suffix = 1;

        while (await userRepository.UsernameExistsAsync(username, cancellationToken))
        {
            username = $"{baseUsername}{suffix}";
            suffix += 1;
        }

        return username;
    }

    private static string? SlugifyUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var characters = value
            .Trim()
            .ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character == '_')
            .Take(30)
            .ToArray();

        if (characters.Length < 3)
        {
            return null;
        }

        return new string(characters);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
