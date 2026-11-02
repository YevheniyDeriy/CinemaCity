using System.Text;
using CinemaCity.API.Auth;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CinemaCity.Tests;

public sealed class CinemaCityApiFactory : WebApplicationFactory<Program>
{
    private const string JwtIssuer = "CinemaCity.Tests";
    private const string JwtAudience = "CinemaCity.Tests";
    private const string JwtSecret = "cinemacity-tests-secret-32-chars-minimum";

    private readonly string _databaseName = $"cinemacity_tests_{Guid.NewGuid():N}";
    private readonly string _connectionString;

    public CinemaCityApiFactory()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CINEMACITY_TEST_POSTGRES") ?? "Host=127.0.0.1;Port=55432;Username=postgres;Password=postgres") { Database = _databaseName };
        _connectionString = connection.ConnectionString;
    }

    public TestSeedIds SeedIds { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:Secret"] = JwtSecret,
                ["Jwt:AccessTokenMinutes"] = "60",
                ["Jwt:RefreshTokenDays"] = "7",
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Google:ClientId"] = "test-google-client",
                ["Google:ClientSecret"] = "test-google-secret"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDatabaseManager>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString));
            services.AddScoped<IDatabaseManager, TestDatabaseManager>();
            services.PostConfigure<JwtOptions>(options =>
            {
                options.Issuer = JwtIssuer;
                options.Audience = JwtAudience;
                options.Secret = JwtSecret;
                options.AccessTokenMinutes = 60;
                options.RefreshTokenDays = 7;
            });
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.ValidIssuer = JwtIssuer;
                options.TokenValidationParameters.ValidAudience = JwtAudience;
                options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
            });
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var owner = new User
        {
            Id = Guid.NewGuid(),
            Username = "review_owner",
            Email = "review.owner@tests.local",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-3)
        };
        owner.PasswordHash = passwordHasher.HashPassword(owner, TestConstants.Password);

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin_user",
            Email = TestConstants.AdminEmail,
            Role = UserRole.Admin,
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-6)
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, TestConstants.Password);

        var genre = new Genre
        {
            Id = 1,
            Name = "Drama",
            Slug = "drama"
        };

        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Public Test Movie",
            OriginalTitle = "Public Test Movie",
            ContentType = ContentType.Movie,
            ReleaseYear = 2024,
            Description = "A movie used by API integration tests.",
            AverageRating = 8.2m,
            RatingsCount = 12,
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-2),
            Genres = [genre]
        };

        var series = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Episode Test Series",
            OriginalTitle = "Episode Test Series",
            ContentType = ContentType.Series,
            ReleaseYear = 2025,
            Description = "A series used by episode rating tests.",
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-1),
            Genres = [genre]
        };

        var season = new Season
        {
            Id = Guid.NewGuid(),
            SeriesId = series.Id,
            SeasonNumber = 1,
            Title = "Season 1",
            ReleaseYear = 2025
        };

        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            EpisodeNumber = 1,
            Title = "Pilot",
            Description = "First episode.",
            AirDate = new DateOnly(2025, 1, 10),
            Duration = 45
        };

        var publicList = new UserMovieList
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            Name = "Public favorites",
            Description = "Visible to everyone.",
            IsPublic = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-20)
        };

        var privateList = new UserMovieList
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            Name = "Private queue",
            Description = "Must never appear on public profile.",
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-19)
        };

        dbContext.Users.AddRange(owner, admin);
        dbContext.Genres.Add(genre);
        dbContext.Movies.AddRange(movie, series);
        dbContext.Seasons.Add(season);
        dbContext.Episodes.Add(episode);
        dbContext.UserMovieLists.AddRange(publicList, privateList);
        dbContext.UserMovieListItems.AddRange(
            new UserMovieListItem
            {
                Id = Guid.NewGuid(),
                UserMovieListId = publicList.Id,
                MovieId = movie.Id,
                AddedAt = DateTimeOffset.UtcNow.AddDays(-18)
            },
            new UserMovieListItem
            {
                Id = Guid.NewGuid(),
                UserMovieListId = privateList.Id,
                MovieId = series.Id,
                AddedAt = DateTimeOffset.UtcNow.AddDays(-17)
            });
        dbContext.Reviews.AddRange(
            new Review
            {
                Id = Guid.NewGuid(),
                UserId = owner.Id,
                MovieId = movie.Id,
                Content = "A public review that should be visible on the profile.",
                Status = ReviewStatus.Approved,
                LikesCount = 3,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-15)
            },
            new Review
            {
                Id = Guid.NewGuid(),
                UserId = owner.Id,
                MovieId = series.Id,
                Content = "A pending review that should stay hidden.",
                Status = ReviewStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-14)
            });

        await dbContext.SaveChangesAsync();

        SeedIds = new TestSeedIds(owner.Id, admin.Id, movie.Id, series.Id, episode.Id, publicList.Id, privateList.Id);
    }

    public async Task DropDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
    }

    private sealed class TestDatabaseManager(AppDbContext dbContext) : IDatabaseManager
    {
        public Task MigrateAsync(CancellationToken cancellationToken = default)
        {
            return dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}

public sealed record TestSeedIds(
    Guid PublicUserId,
    Guid AdminUserId,
    Guid MovieId,
    Guid SeriesId,
    Guid EpisodeId,
    Guid PublicListId,
    Guid PrivateListId);

public static class TestConstants
{
    public const string AdminEmail = "admin@tests.local";
    public const string Password = "TestUser123!";
}
