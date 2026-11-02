using CinemaCity.CatalogService.Data;
using CinemaCity.CatalogService.Endpoints;
using CinemaCity.CatalogService.Imports;
using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Services;
using CinemaCity.CatalogService.Tmdb;
using CinemaCity.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

LocalDevelopmentEnvironment.Load("CinemaCity.CatalogService");
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceDefaults(builder.Configuration, "CinemaCity Catalog Service");
builder.Services.AddLocalJwt(builder.Configuration);
builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddScoped<ICatalogService, CinemaCity.CatalogService.Services.CatalogService>();
builder.Services.AddScoped<ISeriesService, SeriesService>();
builder.Services.AddScoped<ICatalogAdminService, CatalogAdminService>();
builder.Services.AddScoped<ICatalogImportService, CatalogImportService>();
builder.Services.AddScoped<ICatalogSearchService, CatalogSearchService>();
builder.Services.AddScoped<CatalogSnapshotService>();
builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection("Tmdb"));
builder.Services.AddHttpClient<TmdbClient>((provider, client) =>
{
    client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<TmdbOptions>>().Value.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<CommunityClient>(client => { client.BaseAddress = new Uri((builder.Configuration["Services:CommunityUrl"] ?? builder.Configuration["Services:MonolithUrl"]) ?? throw new InvalidOperationException("Configure Services:MonolithUrl.")); client.Timeout = TimeSpan.FromSeconds(10); });
builder.Services.AddScoped<ITmdbImportService, TmdbImportService>();
var app = builder.Build();
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
    await CatalogSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}
app.UseServiceDefaults();
app.UseStaticFiles();
app.MapDatabaseHealth<CatalogDbContext>("CinemaCity.CatalogService");
var api = app.MapGroup("").AddEndpointFilter<ProblemResultFilter>().AddEndpointFilter<ValidationFilter>();
api.MapMovieEndpoints();
api.MapSeriesEndpoints();
api.MapAdminEndpoints();
api.MapSearchEndpoints();
app.MapControllers();
api.MapCatalogInternalEndpoints();
app.Run();

public partial class Program;
