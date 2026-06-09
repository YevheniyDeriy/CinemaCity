using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<MovieCast> MovieCast => Set<MovieCast>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ReviewLike> ReviewLikes => Set<ReviewLike>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<UserMovieList> UserMovieLists => Set<UserMovieList>();
    public DbSet<UserMovieListItem> UserMovieListItems => Set<UserMovieListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureMovies(modelBuilder);
        ConfigureDictionaries(modelBuilder);
        ConfigurePeople(modelBuilder);
        ConfigureSeries(modelBuilder);
        ConfigureRatings(modelBuilder);
        ConfigureReviews(modelBuilder);
        ConfigureWatchlist(modelBuilder);
        ConfigureUserMovieLists(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Id).HasColumnName("id");
            entity.Property(user => user.Username).HasColumnName("username").HasMaxLength(30).IsRequired();
            entity.Property(user => user.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash");
            entity.Property(user => user.GoogleId).HasColumnName("google_id").HasMaxLength(255);
            entity.Property(user => user.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(user => user.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(user => user.IsEmailConfirmed).HasColumnName("is_email_confirmed");
            entity.Property(user => user.FailedLoginAttempts).HasColumnName("failed_login_attempts");
            entity.Property(user => user.LockoutEndAt).HasColumnName("lockout_end_at");
            entity.Property(user => user.CreatedAt).HasColumnName("created_at");
            entity.Property(user => user.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.GoogleId).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.Id).HasColumnName("id");
            entity.Property(token => token.UserId).HasColumnName("user_id");
            entity.Property(token => token.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.Property(token => token.ExpiresAt).HasColumnName("expires_at");
            entity.Property(token => token.RevokedAt).HasColumnName("revoked_at");
            entity.Property(token => token.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureMovies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.ToTable("movies");
            entity.HasKey(movie => movie.Id);
            entity.Property(movie => movie.Id).HasColumnName("id");
            entity.Property(movie => movie.TmdbId).HasColumnName("tmdb_id");
            entity.Property(movie => movie.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(movie => movie.OriginalTitle).HasColumnName("original_title").HasMaxLength(255);
            entity.Property(movie => movie.ContentType).HasColumnName("content_type").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(movie => movie.ReleaseYear).HasColumnName("release_year");
            entity.Property(movie => movie.Duration).HasColumnName("duration");
            entity.Property(movie => movie.Description).HasColumnName("description");
            entity.Property(movie => movie.Status).HasColumnName("status").HasMaxLength(64);
            entity.Property(movie => movie.Tagline).HasColumnName("tagline").HasMaxLength(512);
            entity.Property(movie => movie.FirstAirDate).HasColumnName("first_air_date");
            entity.Property(movie => movie.LastAirDate).HasColumnName("last_air_date");
            entity.Property(movie => movie.SeasonsCount).HasColumnName("seasons_count");
            entity.Property(movie => movie.EpisodesCount).HasColumnName("episodes_count");
            entity.Property(movie => movie.PosterUrl).HasColumnName("poster_url");
            entity.Property(movie => movie.BackdropUrl).HasColumnName("backdrop_url");
            entity.Property(movie => movie.TrailerYoutubeId).HasColumnName("trailer_youtube_id").HasMaxLength(64);
            entity.Property(movie => movie.AverageRating).HasColumnName("average_rating").HasPrecision(3, 1);
            entity.Property(movie => movie.RatingsCount).HasColumnName("ratings_count");
            entity.Property(movie => movie.IsDeleted).HasColumnName("is_deleted");
            entity.Property(movie => movie.DeletedAt).HasColumnName("deleted_at");
            entity.Property(movie => movie.CreatedAt).HasColumnName("created_at");
            entity.Property(movie => movie.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(movie => movie.ContentType);
            entity.HasIndex(movie => movie.ReleaseYear);
            entity.HasIndex(movie => new { movie.AverageRating, movie.RatingsCount });
            entity.HasIndex(movie => movie.IsDeleted);
            entity.HasIndex(movie => new { movie.ContentType, movie.TmdbId }).IsUnique().HasFilter("tmdb_id > 0");
        });
    }

    private static void ConfigureDictionaries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("genres");
            entity.HasKey(genre => genre.Id);
            entity.Property(genre => genre.Id).HasColumnName("id");
            entity.Property(genre => genre.TmdbId).HasColumnName("tmdb_id");
            entity.Property(genre => genre.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(genre => genre.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
            entity.HasIndex(genre => genre.Name).IsUnique();
            entity.HasIndex(genre => genre.Slug).IsUnique();
            entity.HasIndex(genre => genre.TmdbId).IsUnique().HasFilter("tmdb_id > 0");
            entity.HasMany(genre => genre.Movies)
                .WithMany(movie => movie.Genres)
                .UsingEntity<Dictionary<string, object>>(
                    "movie_genres",
                    right => right.HasOne<Movie>().WithMany().HasForeignKey("movie_id").OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<Genre>().WithMany().HasForeignKey("genre_id").OnDelete(DeleteBehavior.Cascade),
                    join => join.HasKey("movie_id", "genre_id"));
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("countries");
            entity.HasKey(country => country.Id);
            entity.Property(country => country.Id).HasColumnName("id");
            entity.Property(country => country.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(country => country.Code).HasColumnName("code").HasMaxLength(2);
            entity.HasIndex(country => country.Name).IsUnique();
            entity.HasIndex(country => country.Code).IsUnique();
            entity.HasMany(country => country.Movies)
                .WithMany(movie => movie.Countries)
                .UsingEntity<Dictionary<string, object>>(
                    "movie_countries",
                    right => right.HasOne<Movie>().WithMany().HasForeignKey("movie_id").OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<Country>().WithMany().HasForeignKey("country_id").OnDelete(DeleteBehavior.Cascade),
                    join => join.HasKey("movie_id", "country_id"));
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.ToTable("languages");
            entity.HasKey(language => language.Id);
            entity.Property(language => language.Id).HasColumnName("id");
            entity.Property(language => language.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(language => language.Code).HasColumnName("code").HasMaxLength(8);
            entity.HasIndex(language => language.Name).IsUnique();
            entity.HasIndex(language => language.Code).IsUnique();
            entity.HasMany(language => language.Movies)
                .WithMany(movie => movie.Languages)
                .UsingEntity<Dictionary<string, object>>(
                    "movie_languages",
                    right => right.HasOne<Movie>().WithMany().HasForeignKey("movie_id").OnDelete(DeleteBehavior.Cascade),
                    left => left.HasOne<Language>().WithMany().HasForeignKey("language_id").OnDelete(DeleteBehavior.Cascade),
                    join => join.HasKey("movie_id", "language_id"));
        });
    }

    private static void ConfigurePeople(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("persons");
            entity.HasKey(person => person.Id);
            entity.Property(person => person.Id).HasColumnName("id");
            entity.Property(person => person.TmdbId).HasColumnName("tmdb_id");
            entity.Property(person => person.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
            entity.Property(person => person.BirthDate).HasColumnName("birth_date");
            entity.Property(person => person.BirthPlace).HasColumnName("birth_place").HasMaxLength(255);
            entity.Property(person => person.Bio).HasColumnName("bio");
            entity.Property(person => person.PhotoUrl).HasColumnName("photo_url");
            entity.Property(person => person.CreatedAt).HasColumnName("created_at");
            entity.Property(person => person.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(person => person.FullName);
            entity.HasIndex(person => person.TmdbId).IsUnique().HasFilter("tmdb_id > 0");
        });

        modelBuilder.Entity<MovieCast>(entity =>
        {
            entity.ToTable("movie_cast");
            entity.HasKey(cast => cast.Id);
            entity.Property(cast => cast.Id).HasColumnName("id");
            entity.Property(cast => cast.MovieId).HasColumnName("movie_id");
            entity.Property(cast => cast.PersonId).HasColumnName("person_id");
            entity.Property(cast => cast.CreditType).HasColumnName("credit_type").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(cast => cast.RoleName).HasColumnName("role_name").HasMaxLength(255);
            entity.Property(cast => cast.SortOrder).HasColumnName("sort_order");
            entity.HasIndex(cast => cast.PersonId);
            entity.HasOne(cast => cast.Movie).WithMany(movie => movie.Cast).HasForeignKey(cast => cast.MovieId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(cast => cast.Person).WithMany(person => person.Credits).HasForeignKey(cast => cast.PersonId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSeries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Season>(entity =>
        {
            entity.ToTable("seasons");
            entity.HasKey(season => season.Id);
            entity.Property(season => season.Id).HasColumnName("id");
            entity.Property(season => season.SeriesId).HasColumnName("series_id");
            entity.Property(season => season.SeasonNumber).HasColumnName("season_number");
            entity.Property(season => season.Title).HasColumnName("title").HasMaxLength(255);
            entity.Property(season => season.Description).HasColumnName("description");
            entity.Property(season => season.ReleaseYear).HasColumnName("release_year");
            entity.HasIndex(season => new { season.SeriesId, season.SeasonNumber }).IsUnique();
            entity.HasOne(season => season.Series).WithMany(movie => movie.Seasons).HasForeignKey(season => season.SeriesId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Episode>(entity =>
        {
            entity.ToTable("episodes");
            entity.HasKey(episode => episode.Id);
            entity.Property(episode => episode.Id).HasColumnName("id");
            entity.Property(episode => episode.SeasonId).HasColumnName("season_id");
            entity.Property(episode => episode.EpisodeNumber).HasColumnName("episode_number");
            entity.Property(episode => episode.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
            entity.Property(episode => episode.Description).HasColumnName("description").HasMaxLength(800);
            entity.Property(episode => episode.AirDate).HasColumnName("air_date");
            entity.Property(episode => episode.Duration).HasColumnName("duration");
            entity.Property(episode => episode.AverageRating).HasColumnName("average_rating").HasPrecision(3, 1);
            entity.Property(episode => episode.RatingsCount).HasColumnName("ratings_count");
            entity.HasIndex(episode => new { episode.SeasonId, episode.EpisodeNumber }).IsUnique();
            entity.HasOne(episode => episode.Season).WithMany(season => season.Episodes).HasForeignKey(episode => episode.SeasonId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRatings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rating>(entity =>
        {
            entity.ToTable("ratings");
            entity.HasKey(rating => rating.Id);
            entity.Property(rating => rating.Id).HasColumnName("id");
            entity.Property(rating => rating.UserId).HasColumnName("user_id");
            entity.Property(rating => rating.MovieId).HasColumnName("movie_id");
            entity.Property(rating => rating.EpisodeId).HasColumnName("episode_id");
            entity.Property(rating => rating.Score).HasColumnName("score").HasPrecision(3, 1);
            entity.Property(rating => rating.CreatedAt).HasColumnName("created_at");
            entity.Property(rating => rating.UpdatedAt).HasColumnName("updated_at");
            entity.ToTable(table => table.HasCheckConstraint("ck_ratings_score", "score >= 1 AND score <= 10"));
            entity.ToTable(table => table.HasCheckConstraint("ck_ratings_single_target", "(movie_id IS NOT NULL AND episode_id IS NULL) OR (movie_id IS NULL AND episode_id IS NOT NULL)"));
            entity.HasIndex(rating => new { rating.UserId, rating.MovieId }).IsUnique().HasFilter("movie_id IS NOT NULL");
            entity.HasIndex(rating => new { rating.UserId, rating.EpisodeId }).IsUnique().HasFilter("episode_id IS NOT NULL");
            entity.HasIndex(rating => rating.MovieId);
            entity.HasOne(rating => rating.User).WithMany(user => user.Ratings).HasForeignKey(rating => rating.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rating => rating.Movie).WithMany(movie => movie.Ratings).HasForeignKey(rating => rating.MovieId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rating => rating.Episode).WithMany(episode => episode.Ratings).HasForeignKey(rating => rating.EpisodeId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureReviews(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");
            entity.HasKey(review => review.Id);
            entity.Property(review => review.Id).HasColumnName("id");
            entity.Property(review => review.UserId).HasColumnName("user_id");
            entity.Property(review => review.MovieId).HasColumnName("movie_id");
            entity.Property(review => review.Content).HasColumnName("content").IsRequired();
            entity.Property(review => review.IsSpoiler).HasColumnName("is_spoiler");
            entity.Property(review => review.LikesCount).HasColumnName("likes_count");
            entity.Property(review => review.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(review => review.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(review => review.IsDeleted).HasColumnName("is_deleted");
            entity.Property(review => review.DeletedAt).HasColumnName("deleted_at");
            entity.Property(review => review.CreatedAt).HasColumnName("created_at");
            entity.Property(review => review.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(review => new { review.UserId, review.MovieId }).IsUnique();
            entity.HasIndex(review => new { review.MovieId, review.Status });
            entity.HasIndex(review => new { review.Status, review.CreatedAt });
            entity.HasOne(review => review.User).WithMany(user => user.Reviews).HasForeignKey(review => review.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(review => review.Movie).WithMany(movie => movie.Reviews).HasForeignKey(review => review.MovieId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("comments");
            entity.HasKey(comment => comment.Id);
            entity.Property(comment => comment.Id).HasColumnName("id");
            entity.Property(comment => comment.ReviewId).HasColumnName("review_id");
            entity.Property(comment => comment.UserId).HasColumnName("user_id");
            entity.Property(comment => comment.Content).HasColumnName("content").IsRequired();
            entity.Property(comment => comment.IsDeleted).HasColumnName("is_deleted");
            entity.Property(comment => comment.CreatedAt).HasColumnName("created_at");
            entity.Property(comment => comment.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(comment => comment.ReviewId);
            entity.HasOne(comment => comment.Review).WithMany(review => review.Comments).HasForeignKey(comment => comment.ReviewId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(comment => comment.User).WithMany(user => user.Comments).HasForeignKey(comment => comment.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReviewLike>(entity =>
        {
            entity.ToTable("review_likes");
            entity.HasKey(like => new { like.UserId, like.ReviewId });
            entity.Property(like => like.UserId).HasColumnName("user_id");
            entity.Property(like => like.ReviewId).HasColumnName("review_id");
            entity.Property(like => like.CreatedAt).HasColumnName("created_at");
            entity.HasOne(like => like.User).WithMany(user => user.ReviewLikes).HasForeignKey(like => like.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(like => like.Review).WithMany(review => review.Likes).HasForeignKey(like => like.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureWatchlist(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.ToTable("watchlist_items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserId).HasColumnName("user_id");
            entity.Property(item => item.MovieId).HasColumnName("movie_id");
            entity.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(item => item.AddedAt).HasColumnName("added_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(item => new { item.UserId, item.MovieId }).IsUnique();
            entity.HasIndex(item => new { item.UserId, item.Status });
            entity.HasOne(item => item.User).WithMany(user => user.WatchlistItems).HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Movie).WithMany(movie => movie.WatchlistItems).HasForeignKey(item => item.MovieId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUserMovieLists(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserMovieList>(entity =>
        {
            entity.ToTable("user_movie_lists");
            entity.HasKey(list => list.Id);
            entity.Property(list => list.Id).HasColumnName("id");
            entity.Property(list => list.UserId).HasColumnName("user_id");
            entity.Property(list => list.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
            entity.Property(list => list.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(list => list.IsPublic).HasColumnName("is_public");
            entity.Property(list => list.CreatedAt).HasColumnName("created_at");
            entity.Property(list => list.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(list => new { list.UserId, list.Name }).IsUnique();
            entity.HasOne(list => list.User).WithMany(user => user.MovieLists).HasForeignKey(list => list.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserMovieListItem>(entity =>
        {
            entity.ToTable("user_movie_list_items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UserMovieListId).HasColumnName("user_movie_list_id");
            entity.Property(item => item.MovieId).HasColumnName("movie_id");
            entity.Property(item => item.AddedAt).HasColumnName("added_at");
            entity.HasIndex(item => item.MovieId);
            entity.HasIndex(item => new { item.UserMovieListId, item.MovieId }).IsUnique();
            entity.HasOne(item => item.UserMovieList).WithMany(list => list.Items).HasForeignKey(item => item.UserMovieListId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Movie).WithMany(movie => movie.UserMovieListItems).HasForeignKey(item => item.MovieId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
