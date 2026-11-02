using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace CinemaCity.CatalogService.Data;
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? "Host=localhost;Database=cinemacity_catalog;Username=postgres").Options);
}
