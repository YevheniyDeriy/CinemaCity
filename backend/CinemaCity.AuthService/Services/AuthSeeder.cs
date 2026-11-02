using CinemaCity.AuthService.Data;
using CinemaCity.AuthService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.AuthService.Services;

public static class AuthSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var db = services.GetRequiredService<AuthDbContext>();
        // Roles are stable migration seed; an administrator is only created from supplied configuration.
        var email = configuration["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var password = configuration["Seed:AdminPassword"];
        if (!configuration.GetValue("Seed:Enabled", false) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (password.Length < 8) throw new InvalidOperationException("Seed:AdminPassword must contain at least 8 characters.");
        if (await db.Users.AnyAsync(user => user.Email == email)) return;
        var user = new User { Id = Guid.NewGuid(), Email = email, Username = configuration["Seed:AdminUsername"] ?? "admin", Role = UserRole.Admin, IsActive = true };
        user.PasswordHash = services.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password);
        user.Roles.Add(new UserRoleAssignment { UserId = user.Id, RoleId = 2 });
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
