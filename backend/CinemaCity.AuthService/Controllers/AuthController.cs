using CinemaCity.AuthService.Dtos;
using CinemaCity.AuthService.Services;
using CinemaCity.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CinemaCity.AuthService.Controllers;

/// <summary>Thin HTTP adapter for authentication workflows extracted from CinemaCity.AuthEndpoints.</summary>
[ApiController, Route("api/v1/auth"), Tags("Auth")]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    /// <summary>Register a CinemaCity account with the User role.</summary>
    [HttpPost("register"), AllowAnonymous]
    [ProducesResponseType<AuthResponse>(201), ProducesResponseType<ValidationProblemDetails>(400), ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Register(RegisterDto request, CancellationToken ct) => new HttpResultAction(await service.RegisterAsync(request.ToRequest(), HttpContext, ct));

    /// <summary>Sign in and issue access/refresh tokens, preserving the original browser session cookie.</summary>
    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType<AuthResponse>(200), ProducesResponseType<ValidationProblemDetails>(400), ProducesResponseType(401), ProducesResponseType<ProblemDetails>(423)]
    public async Task<IActionResult> Login(LoginDto request, CancellationToken ct) => new HttpResultAction(await service.LoginAsync(new LoginRequest(request.Email, request.Password), HttpContext, ct));

    /// <summary>Rotate a refresh token and invalidate its previous value.</summary>
    [HttpPost("refresh"), AllowAnonymous]
    [ProducesResponseType<AuthResponse>(200), ProducesResponseType<ValidationProblemDetails>(400), ProducesResponseType(401)]
    public async Task<IActionResult> Refresh([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshDto? request, CancellationToken ct) => new HttpResultAction(await service.RefreshAsync(new RefreshRequest(request?.RefreshToken), HttpContext, ct));

    /// <summary>Revoke the current account's refresh token.</summary>
    [HttpPost("logout"), Authorize]
    [ProducesResponseType(204), ProducesResponseType<ValidationProblemDetails>(400), ProducesResponseType(401)]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshDto? request, CancellationToken ct) => new HttpResultAction(await service.LogoutAsync(new RefreshRequest(request?.RefreshToken), HttpContext, ct));

    /// <summary>Original frontend compatibility alias for the current token identity.</summary>
    [HttpGet("me"), Authorize]
    [ProducesResponseType<AuthUserResponse>(200), ProducesResponseType(401)]
    public IActionResult Me() => new HttpResultAction(service.Me(User));

    /// <summary>Begin the original CinemaCity Google sign-in through the gateway origin.</summary>
    [HttpGet("google"), AllowAnonymous]
    [ProducesResponseType(302), ProducesResponseType<ProblemDetails>(503)]
    public IActionResult Google([FromQuery] string? returnUrl = null) => new HttpResultAction(service.GoogleLogin(HttpContext, returnUrl));

    /// <summary>Complete Google sign-in and return only to a configured frontend origin.</summary>
    [HttpGet("google/callback"), AllowAnonymous]
    [ProducesResponseType<AuthResponse>(200), ProducesResponseType(302), ProducesResponseType<ValidationProblemDetails>(400)]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl, CancellationToken ct) => new HttpResultAction(await service.GoogleCallbackAsync(HttpContext, returnUrl, ct));
}
