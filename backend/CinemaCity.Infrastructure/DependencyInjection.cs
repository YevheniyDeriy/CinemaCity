using CinemaCity.Application.Imports;
using CinemaCity.Infrastructure.Data;
using CinemaCity.Infrastructure.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CinemaCity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.Configure<TmdbOptions>(configuration.GetSection(TmdbOptions.SectionName));
        services.AddHttpClient<TmdbClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TmdbOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ITmdbImportService, TmdbImportService>();

        return services;
    }
}
