using Bogus;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API;

public static class DevelopmentDataSeeder
{
    private const string TestEmailDomain = "cinemacity.local";
    private const string TestReviewPrefix = "[DEV-SEED]";
    private const string TestPassword = "DevUser123!";

    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        if (await dbContext.Users.AnyAsync(user => user.Email.EndsWith($"@{TestEmailDomain}"), cancellationToken))
        {
            logger.LogInformation("Development seed users already exist. Skipping development data seeding.");
            return;
        }

        var movies = await dbContext.Movies
            .Where(movie => !movie.IsDeleted)
            .OrderByDescending(movie => movie.AverageRating)
            .Take(40)
            .ToListAsync(cancellationToken);

        if (!movies.Any())
        {
            logger.LogInformation("No movies found for development data seeding.");
            return;
        }

        var faker = new Faker("uk");
        var users = CreateUsers(passwordHasher, faker);
        dbContext.Users.AddRange(users);

        var ratings = new List<Rating>();
        var reviews = new List<Review>();
        var lists = new List<UserMovieList>();
        var listItems = new List<UserMovieListItem>();

        foreach (var user in users)
        {
            var userMovies = faker.PickRandom(movies, Math.Min(movies.Count, faker.Random.Int(8, 16))).DistinctBy(movie => movie.Id).ToList();
            var createdAt = faker.Date.BetweenOffset(DateTimeOffset.UtcNow.AddMonths(-8), DateTimeOffset.UtcNow.AddDays(-2));

            var watchLater = new UserMovieList
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "Перегляну пізніше",
                Description = "Тестовий список для Development seed.",
                IsPublic = false,
                CreatedAt = createdAt
            };

            var favorites = new UserMovieList
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "Улюблене",
                Description = "Тестовий список для Development seed.",
                IsPublic = true,
                CreatedAt = createdAt.AddDays(faker.Random.Int(1, 10))
            };

            lists.Add(watchLater);
            lists.Add(favorites);

            foreach (var movie in userMovies)
            {
                var ratingCreatedAt = faker.Date.BetweenOffset(createdAt, DateTimeOffset.UtcNow.AddDays(-1));
                ratings.Add(new Rating
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    MovieId = movie.Id,
                    Score = faker.Random.Decimal(1, 10),
                    CreatedAt = ratingCreatedAt
                });

                if (faker.Random.Bool(0.35f))
                {
                    reviews.Add(new Review
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        MovieId = movie.Id,
                        Content = $"{TestReviewPrefix} {CreateReviewText(faker, movie.Title)}",
                        IsSpoiler = faker.Random.Bool(0.12f),
                        LikesCount = faker.Random.Int(0, 30),
                        Status = ReviewStatus.Approved,
                        CreatedAt = ratingCreatedAt.AddHours(faker.Random.Int(2, 72))
                    });
                }
            }

            foreach (var movie in faker.PickRandom(movies, Math.Min(movies.Count, faker.Random.Int(4, 8))).DistinctBy(movie => movie.Id))
            {
                listItems.Add(new UserMovieListItem
                {
                    Id = Guid.NewGuid(),
                    UserMovieListId = watchLater.Id,
                    MovieId = movie.Id,
                    AddedAt = faker.Date.BetweenOffset(watchLater.CreatedAt, DateTimeOffset.UtcNow)
                });
            }

            foreach (var movie in faker.PickRandom(movies, Math.Min(movies.Count, faker.Random.Int(3, 6))).DistinctBy(movie => movie.Id))
            {
                listItems.Add(new UserMovieListItem
                {
                    Id = Guid.NewGuid(),
                    UserMovieListId = favorites.Id,
                    MovieId = movie.Id,
                    AddedAt = faker.Date.BetweenOffset(favorites.CreatedAt, DateTimeOffset.UtcNow)
                });
            }
        }

        dbContext.Ratings.AddRange(ratings.DistinctBy(rating => new { rating.UserId, rating.MovieId }));
        dbContext.Reviews.AddRange(reviews.DistinctBy(review => new { review.UserId, review.MovieId }));
        dbContext.UserMovieLists.AddRange(lists);
        dbContext.UserMovieListItems.AddRange(listItems.DistinctBy(item => new { item.UserMovieListId, item.MovieId }));

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {UsersCount} development users, {RatingsCount} ratings and {ReviewsCount} reviews.", users.Count, ratings.Count, reviews.Count);
    }

    private static List<User> CreateUsers(IPasswordHasher<User> passwordHasher, Faker faker)
    {
        var users = new List<User>();

        for (var index = 1; index <= 10; index++)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = $"dev_user_{index}",
                Email = $"dev.user{index}@{TestEmailDomain}",
                Role = UserRole.User,
                IsEmailConfirmed = true,
                AvatarUrl = null,
                CreatedAt = faker.Date.BetweenOffset(DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow.AddMonths(-1))
            };

            user.PasswordHash = passwordHasher.HashPassword(user, TestPassword);
            users.Add(user);
        }

        return users;
    }

    private static string CreateReviewText(Faker faker, string title)
    {
        var templates = new[]
        {
            "«{0}» добре тримає темп і має кілька сильних сцен.",
            "Після перегляду «{0}» хочеться повернутися до окремих моментів ще раз.",
            "У «{0}» особливо спрацювали акторські роботи та атмосфера.",
            "«{0}» не ідеальний, але точно вартий уваги для вечірнього перегляду.",
            "Найсильніше у «{0}» — настрій, музика і фінальний акт."
        };

        return string.Format(faker.PickRandom(templates), title);
    }
}
