using CinemaCity.ApiGateway;
using CinemaCity.ServiceDefaults;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

LocalDevelopmentEnvironment.Load("CinemaCity.ApiGateway");
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonStream(GatewayConfiguration.Load(builder.Configuration, builder.Environment.ContentRootPath));
builder.Services.AddHttpContextAccessor();
builder.Services.AddLocalJwt(builder.Configuration);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CatalogCache>();
builder.Services.AddScoped<ScreenClients>();
builder.Services.AddScoped<IScreenComposer, MovieScreenComposer>();
builder.Services.AddRateLimiter(options => GatewayPolicies.ConfigureLimits(options, builder.Configuration));
foreach (var service in new[] { "Auth", "Catalog", "Community" })
    builder.Services.AddHttpClient(service, client =>
    {
        client.BaseAddress = new Uri(builder.Configuration[$"Services:{service}Url"]!);
        client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Gateway:TimeoutSeconds", 8));
    });
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("Retry-After", "X-Correlation-Id", "Location")));
builder.Services.AddOcelot(builder.Configuration).AddDelegatingHandler<DownstreamLoggingHandler>(true);
var app = builder.Build();
app.UseExceptionHandler(error => error.Run(context => GatewayPolicies.Problem(context, 500, "An unexpected gateway error occurred", context.RequestAborted)));
app.UseMiddleware<ProblemResponseMiddleware>();
app.UseCors();
app.UseMiddleware<GatewayMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/openapi/gateway.json")
    {
        context.Response.ContentType = "application/json";
        await context.Response.SendFileAsync(Path.Combine(app.Environment.ContentRootPath, "gateway-openapi.json"), context.RequestAborted);
        return;
    }
    await next(context);
});
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/gateway.json", "CinemaCity Gateway v1"));
app.UseMiddleware<GatewayFeaturesMiddleware>();
await app.UseOcelot();
await app.RunAsync();

public partial class Program;
