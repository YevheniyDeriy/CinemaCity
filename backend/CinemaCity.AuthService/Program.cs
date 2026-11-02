using CinemaCity.AuthService.Data;
using CinemaCity.AuthService.Endpoints;
using CinemaCity.AuthService.Models;
using CinemaCity.AuthService.Repositories;
using CinemaCity.AuthService.Services;
using CinemaCity.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

LocalDevelopmentEnvironment.Load("CinemaCity.AuthService");
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceDefaults(builder.Configuration, "CinemaCity Auth Service");
builder.Services.AddLocalJwt(builder.Configuration);
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddDbContext<AuthDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, CinemaCity.AuthService.Services.AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
var authentication = builder.Services.AddAuthentication().AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
if (!string.IsNullOrWhiteSpace(builder.Configuration["Google:ClientId"]))
    authentication.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.ClientId = builder.Configuration["Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
        options.CallbackPath = builder.Configuration["Google:CallbackPath"] ?? "/api/auth/google/signin";
    });
var app = builder.Build();
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
    await AuthSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}
app.UseServiceDefaults(normalizeErrors: true);
app.MapDatabaseHealth<AuthDbContext>("CinemaCity.AuthService");
app.MapControllers();
app.MapUserEndpoints();
app.Run();

public partial class Program;
