using Microsoft.Extensions.DependencyInjection;

namespace CinemaCity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
