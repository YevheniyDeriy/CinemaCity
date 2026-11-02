using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace CinemaCity.AuthService.Data;
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? "Host=localhost;Database=cinemacity_auth;Username=postgres").Options);
}
