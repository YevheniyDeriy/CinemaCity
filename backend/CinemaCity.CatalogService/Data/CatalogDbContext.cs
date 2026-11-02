using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.CatalogService.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<MovieCast> MovieCast => Set<MovieCast>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Episode> Episodes => Set<Episode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMovies(modelBuilder);
        ConfigureDictionaries(modelBuilder);
        ConfigurePeople(modelBuilder);
        ConfigureSeries(modelBuilder);
        modelBuilder.Entity<CommunityStatisticsVersion>(entity =>
        {
            entity.ToTable("community_statistics_versions");
            entity.HasKey(v => new { v.Kind, v.TargetId });
            entity.Property(v => v.Kind).HasMaxLength(20);
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
            entity.Property(movie => movie.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(movie => movie.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(movie => movie.Title);
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
            entity.Property(person => person.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
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
            entity.Property(episode => episode.Description).HasColumnName("description");
            entity.Property(episode => episode.AirDate).HasColumnName("air_date");
            entity.Property(episode => episode.Duration).HasColumnName("duration");
            entity.Property(episode => episode.AverageRating).HasColumnName("average_rating").HasPrecision(3, 1);
            entity.Property(episode => episode.RatingsCount).HasColumnName("ratings_count");
            entity.HasIndex(episode => new { episode.SeasonId, episode.EpisodeNumber }).IsUnique();
            entity.HasOne(episode => episode.Season).WithMany(season => season.Episodes).HasForeignKey(episode => episode.SeasonId).OnDelete(DeleteBehavior.Cascade);
        });
    }








}
