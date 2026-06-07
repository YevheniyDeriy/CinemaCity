using CinemaCity.API.Admin;
using CinemaCity.API.Auth;
using CinemaCity.API.Home;
using CinemaCity.API.Movies;
using CinemaCity.API.Profile;
using CinemaCity.API.Ratings;
using CinemaCity.API.Reviews;
using CinemaCity.API.Search;
using CinemaCity.API.Series;
using CinemaCity.Application;
using CinemaCity.Infrastructure;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCinemaCityAuth(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174", "http://127.0.0.1:5174")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CinemaCity API",
        Version = "v1",
        Description = "API для каталогу, автентифікації та керування CinemaCity."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Вставте JWT access token з відповіді login без префікса Bearer."
    });

});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CinemaCity API v1");
        options.DocumentTitle = "CinemaCity API";
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    Application = "CinemaCity API",
    Version = "1.0"
}));

app.MapGet("/health/database", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { Database = "PostgreSQL", Status = "Available" })
        : Results.Problem("PostgreSQL is unavailable");
})
.WithName("DatabaseHealth");

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapHomeEndpoints();
app.MapMovieEndpoints();
app.MapSeriesEndpoints();
app.MapRatingEndpoints();
app.MapReviewEndpoints();
app.MapProfileEndpoints();
app.MapSearchEndpoints();

app.Run();
