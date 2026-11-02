using System.Security.Claims;
using CinemaCity.AuthService.Repositories;
using CinemaCity.AuthService.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using CinemaCity.ServiceDefaults;
namespace CinemaCity.AuthService.Services;

public interface IAuthService
{
    Task<IResult> RegisterAsync(RegisterRequest request, HttpContext httpContext, CancellationToken cancellationToken);
    Task<IResult> LoginAsync(LoginRequest request, HttpContext httpContext, CancellationToken cancellationToken);
    Task<IResult> RefreshAsync(RefreshRequest? request, HttpContext httpContext, CancellationToken cancellationToken);
    Task<IResult> LogoutAsync(RefreshRequest? request, HttpContext httpContext, CancellationToken cancellationToken);
    Ok<AuthUserResponse> Me(ClaimsPrincipal principal);
    IResult GoogleLogin(HttpContext httpContext, string? returnUrl = null);
    Task<IResult> GoogleCallbackAsync(HttpContext httpContext, string? returnUrl, CancellationToken cancellationToken);
}

public sealed class AuthService(IUserRepository userRepository, IRefreshTokenRepository refreshTokenRepository, IUnitOfWork unitOfWork, IPasswordHasher<User> passwordHasher, TokenService tokenService, IOptions<JwtOptions> jwtOptions, ILogger<AuthService> logger) : IAuthService
{
    public async Task<IResult> RegisterAsync(RegisterRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await AuthEndpointsWorkflow.RegisterAsync(request, httpContext, userRepository, refreshTokenRepository, unitOfWork, passwordHasher, tokenService, jwtOptions, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("RegisterAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> LoginAsync(LoginRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await AuthEndpointsWorkflow.LoginAsync(request, httpContext, userRepository, refreshTokenRepository, unitOfWork, passwordHasher, tokenService, jwtOptions, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("LoginAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> RefreshAsync(RefreshRequest? request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await AuthEndpointsWorkflow.RefreshAsync(request, httpContext, refreshTokenRepository, unitOfWork, tokenService, jwtOptions, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("RefreshAsync completed with status {Status}", status);
        return result;
    }
    public async Task<IResult> LogoutAsync(RefreshRequest? request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await AuthEndpointsWorkflow.LogoutAsync(request, httpContext, refreshTokenRepository, unitOfWork, cancellationToken);
        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? 200;
        logger.LogInformation("LogoutAsync completed with status {Status}", status);
        return result;
    }
    public Ok<AuthUserResponse> Me(ClaimsPrincipal principal) => AuthEndpointsWorkflow.Me(principal);
    public IResult GoogleLogin(HttpContext httpContext, string? returnUrl = null) => AuthEndpointsWorkflow.GoogleLogin(httpContext, returnUrl);
    public Task<IResult> GoogleCallbackAsync(HttpContext httpContext, string? returnUrl, CancellationToken cancellationToken) => AuthEndpointsWorkflow.GoogleCallbackAsync(httpContext, userRepository, refreshTokenRepository, unitOfWork, tokenService, jwtOptions, returnUrl, cancellationToken);
}
