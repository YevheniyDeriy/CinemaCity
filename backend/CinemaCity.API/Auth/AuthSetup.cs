using System.Text;
using System.Security.Claims;
using CinemaCity.Domain.Enums;
using CinemaCity.Domain.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace CinemaCity.API.Auth;

public static class AuthSetup
{
    public const string RefreshTokenCookieName = "refresh_token";

    public static IServiceCollection AddCinemaCityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Secret) && Encoding.UTF8.GetByteCount(options.Secret) >= 32, "Jwt:Secret must contain at least 32 bytes.")
            .ValidateOnStart();
        services.AddScoped<TokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, configured) =>
            {
                var jwtOptions = configured.Value;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        if (!string.IsNullOrWhiteSpace(configuration["Google:ClientId"]))
            services.AddAuthentication().AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.ClientId = configuration["Google:ClientId"] ?? string.Empty;
                options.ClientSecret = configuration["Google:ClientSecret"] ?? string.Empty;
                options.CallbackPath = configuration["Google:CallbackPath"] ?? "/api/auth/google/callback";
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRole.Admin.ToString()));
        });

        return services;
    }
}
