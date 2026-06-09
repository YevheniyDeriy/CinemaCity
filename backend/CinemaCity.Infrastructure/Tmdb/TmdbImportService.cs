using System.Text;
using System.Text.RegularExpressions;
using CinemaCity.Application.Imports;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CinemaCity.Infrastructure.Tmdb;

internal sealed partial class TmdbImportService(
    TmdbClient tmdbClient,
    AppDbContext dbContext,
    ILogger<TmdbImportService> logger) : ITmdbImportService
{
    private const string PosterBaseUrl = "https://image.tmdb.org/t/p/w500";
    private const string BackdropBaseUrl = "https://image.tmdb.org/t/p/original";
    private const int MaxCreditsPerTitle = 24;

    public async Task<ImportResultDto> ImportPopularMoviesAsync(
        int pages,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportAccumulator();
        logger.LogInformation("Starting TMDB popular movie import for {Pages} pages.", pages);

        for (var page = 1; page <= pages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Importing TMDB popular movie page {Page}.", page);

            try
            {
                var popular = await tmdbClient.GetPopularMoviesAsync(page, cancellationToken);
                var items = popular.Results?.Where(item => item.Id > 0).ToList() ?? [];
                result.Fetched += items.Count;

                foreach (var item in items)
                {
                    await ImportMovieByTmdbIdAsync(item.Id, result, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "TMDB popular movie page {Page} failed.", page);
                result.AddError($"Movie page {page}: {FormatException(exception)}");
            }
        }

        logger.LogInformation(
            "Finished TMDB popular movie import. Added: {Added}, updated: {Updated}, skipped: {Skipped}, errors: {Errors}.",
            result.Added,
            result.Updated,
            result.Skipped,
            result.ErrorsCount);

        return result.ToDto();
    }

    public async Task<ImportResultDto> ImportPopularTvShowsAsync(
        int pages,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportAccumulator();
        logger.LogInformation("Starting TMDB popular TV show import for {Pages} pages.", pages);

        for (var page = 1; page <= pages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Importing TMDB popular TV show page {Page}.", page);

            try
            {
                var popular = await tmdbClient.GetPopularTvShowsAsync(page, cancellationToken);
                var items = popular.Results?.Where(item => item.Id > 0).ToList() ?? [];
                result.Fetched += items.Count;

                foreach (var item in items)
                {
                    await ImportTvShowByTmdbIdAsync(item.Id, result, cancellationToken);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "TMDB popular TV show page {Page} failed.", page);
                result.AddError($"TV page {page}: {FormatException(exception)}");
            }
        }

        logger.LogInformation(
            "Finished TMDB popular TV show import. Added: {Added}, updated: {Updated}, skipped: {Skipped}, errors: {Errors}.",
            result.Added,
            result.Updated,
            result.Skipped,
            result.ErrorsCount);

        return result.ToDto();
    }

    public async Task<ImportResultDto> ImportMovieDetailsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportAccumulator { Fetched = 1 };
        logger.LogInformation("Starting TMDB movie detail import for TMDB ID {TmdbId}.", tmdbId);

        await ImportMovieByTmdbIdAsync(tmdbId, result, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Finished TMDB movie detail import for TMDB ID {TmdbId}. Added: {Added}, updated: {Updated}, skipped: {Skipped}, errors: {Errors}.",
            tmdbId,
            result.Added,
            result.Updated,
            result.Skipped,
            result.ErrorsCount);

        return result.ToDto();
    }

    private async Task ImportMovieByTmdbIdAsync(int tmdbId, ImportAccumulator result, CancellationToken cancellationToken)
    {
        try
        {
            var details = await tmdbClient.GetMovieDetailsAsync(tmdbId, cancellationToken);
            var credits = await tmdbClient.GetMovieCreditsAsync(tmdbId, cancellationToken);
            await UpsertMovieAsync(details, credits, result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "TMDB movie {TmdbId} failed.", tmdbId);
            result.AddError($"Movie {tmdbId}: {FormatException(exception)}");
        }
    }

    private async Task ImportTvShowByTmdbIdAsync(int tmdbId, ImportAccumulator result, CancellationToken cancellationToken)
    {
        try
        {
            var details = await tmdbClient.GetTvShowDetailsAsync(tmdbId, cancellationToken);
            var credits = await tmdbClient.GetTvShowCreditsAsync(tmdbId, cancellationToken);
            await UpsertTvShowAsync(details, credits, result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "TMDB TV show {TmdbId} failed.", tmdbId);
            result.AddError($"TV {tmdbId}: {FormatException(exception)}");
        }
    }

    private async Task UpsertMovieAsync(
        TmdbMovieDetails details,
        TmdbCredits credits,
        ImportAccumulator result,
        CancellationToken cancellationToken)
    {
        var title = TrimOrNull(details.Title);
        if (details.Id <= 0 || title is null)
        {
            result.Skipped++;
            result.AddError($"Movie {details.Id}: required title or TMDB ID is missing.");
            return;
        }

        var movie = await LoadMovieAsync(ContentType.Movie, details.Id, cancellationToken);
        var isNew = movie is null;
        movie ??= new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = details.Id,
            ContentType = ContentType.Movie,
            CreatedAt = DateTimeOffset.UtcNow
        };

        movie.Title = title;
        movie.OriginalTitle = TrimOrNull(details.OriginalTitle);
        movie.ReleaseYear = details.ReleaseDate?.Year ?? 0;
        movie.Duration = details.Runtime;
        movie.Description = TrimOrNull(details.Overview);
        movie.Status = TrimOrNull(details.Status);
        movie.Tagline = TrimOrNull(details.Tagline);
        movie.FirstAirDate = null;
        movie.LastAirDate = null;
        movie.SeasonsCount = null;
        movie.EpisodesCount = null;
        movie.PosterUrl = BuildPosterUrl(details.PosterPath);
        movie.BackdropUrl = BuildBackdropUrl(details.BackdropPath);
        movie.AverageRating = RoundRating(details.VoteAverage);
        movie.RatingsCount = details.VoteCount;
        movie.IsDeleted = false;
        movie.DeletedAt = null;
        movie.UpdatedAt = isNew ? null : DateTimeOffset.UtcNow;

        await SyncDictionariesAsync(movie, details.Genres, details.ProductionCountries, details.SpokenLanguages, cancellationToken);
        await SyncCreditsAsync(movie, credits, cancellationToken);

        if (isNew)
        {
            dbContext.Movies.Add(movie);
            result.Added++;
        }
        else
        {
            result.Updated++;
        }
    }

    private async Task UpsertTvShowAsync(
        TmdbTvShowDetails details,
        TmdbCredits credits,
        ImportAccumulator result,
        CancellationToken cancellationToken)
    {
        var title = TrimOrNull(details.Name);
        if (details.Id <= 0 || title is null)
        {
            result.Skipped++;
            result.AddError($"TV {details.Id}: required title or TMDB ID is missing.");
            return;
        }

        var series = await LoadMovieAsync(ContentType.Series, details.Id, cancellationToken);
        var isNew = series is null;
        series ??= new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = details.Id,
            ContentType = ContentType.Series,
            CreatedAt = DateTimeOffset.UtcNow
        };

        series.Title = title;
        series.OriginalTitle = TrimOrNull(details.OriginalName);
        series.ReleaseYear = details.FirstAirDate?.Year ?? 0;
        series.Duration = null;
        series.Description = TrimOrNull(details.Overview);
        series.Status = TrimOrNull(details.Status);
        series.Tagline = TrimOrNull(details.Tagline);
        series.FirstAirDate = details.FirstAirDate;
        series.LastAirDate = details.LastAirDate;
        series.SeasonsCount = details.NumberOfSeasons;
        series.EpisodesCount = details.NumberOfEpisodes;
        series.PosterUrl = BuildPosterUrl(details.PosterPath);
        series.BackdropUrl = BuildBackdropUrl(details.BackdropPath);
        series.AverageRating = RoundRating(details.VoteAverage);
        series.RatingsCount = details.VoteCount;
        series.IsDeleted = false;
        series.DeletedAt = null;
        series.UpdatedAt = isNew ? null : DateTimeOffset.UtcNow;

        await SyncDictionariesAsync(series, details.Genres, details.ProductionCountries, details.SpokenLanguages, cancellationToken);
        await SyncCreditsAsync(series, credits, cancellationToken);

        if (isNew)
        {
            dbContext.Movies.Add(series);
            result.Added++;
        }
        else
        {
            result.Updated++;
        }
    }

    private Task<Movie?> LoadMovieAsync(ContentType contentType, int tmdbId, CancellationToken cancellationToken)
    {
        return dbContext.Movies
            .Include(movie => movie.Genres)
            .Include(movie => movie.Countries)
            .Include(movie => movie.Languages)
            .FirstOrDefaultAsync(
                movie => movie.ContentType == contentType && movie.TmdbId == tmdbId,
                cancellationToken);
    }

    private async Task SyncDictionariesAsync(
        Movie movie,
        IReadOnlyCollection<TmdbGenre>? tmdbGenres,
        IReadOnlyCollection<TmdbCountry>? tmdbCountries,
        IReadOnlyCollection<TmdbLanguage>? tmdbLanguages,
        CancellationToken cancellationToken)
    {
        movie.Genres.Clear();
        foreach (var tmdbGenre in tmdbGenres ?? [])
        {
            var genre = await GetOrCreateGenreAsync(tmdbGenre, cancellationToken);
            if (genre is not null)
            {
                movie.Genres.Add(genre);
            }
        }

        movie.Countries.Clear();
        foreach (var tmdbCountry in tmdbCountries ?? [])
        {
            var country = await GetOrCreateCountryAsync(tmdbCountry, cancellationToken);
            if (country is not null)
            {
                movie.Countries.Add(country);
            }
        }

        movie.Languages.Clear();
        foreach (var tmdbLanguage in tmdbLanguages ?? [])
        {
            var language = await GetOrCreateLanguageAsync(tmdbLanguage, cancellationToken);
            if (language is not null)
            {
                movie.Languages.Add(language);
            }
        }
    }

    private async Task SyncCreditsAsync(Movie movie, TmdbCredits credits, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(movie).State != EntityState.Added)
        {
            await dbContext.MovieCast
                .Where(cast => cast.MovieId == movie.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var uniqueCredits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sortOrder = 0;

        foreach (var castMember in (credits.Cast ?? [])
            .Where(member => member.Id > 0 && !string.IsNullOrWhiteSpace(member.Name))
            .OrderBy(member => member.Order ?? int.MaxValue)
            .Take(MaxCreditsPerTitle))
        {
            var person = await GetOrCreatePersonAsync(castMember.Id, castMember.Name!, castMember.ProfilePath, cancellationToken);
            var roleName = TrimOrNull(castMember.Character);
            var key = $"{person.TmdbId}:actor:{roleName}";

            if (!uniqueCredits.Add(key))
            {
                continue;
            }

            dbContext.MovieCast.Add(new MovieCast
            {
                Id = Guid.NewGuid(),
                MovieId = movie.Id,
                PersonId = person.Id,
                CreditType = CreditType.Actor,
                RoleName = roleName,
                SortOrder = castMember.Order ?? sortOrder
            });
            sortOrder++;
        }

        foreach (var crewMember in (credits.Crew ?? [])
            .Where(member => member.Id > 0 && !string.IsNullOrWhiteSpace(member.Name))
            .Select(member => new { Member = member, CreditType = MapCreditType(member.Job, member.Department) })
            .Where(item => item.CreditType is not null)
            .Take(MaxCreditsPerTitle))
        {
            var creditType = crewMember.CreditType.GetValueOrDefault();
            var person = await GetOrCreatePersonAsync(crewMember.Member.Id, crewMember.Member.Name!, crewMember.Member.ProfilePath, cancellationToken);
            var roleName = TrimOrNull(crewMember.Member.Job);
            var key = $"{person.TmdbId}:{creditType}:{roleName}";

            if (!uniqueCredits.Add(key))
            {
                continue;
            }

            dbContext.MovieCast.Add(new MovieCast
            {
                Id = Guid.NewGuid(),
                MovieId = movie.Id,
                PersonId = person.Id,
                CreditType = creditType,
                RoleName = roleName,
                SortOrder = sortOrder
            });
            sortOrder++;
        }
    }

    private async Task<Genre?> GetOrCreateGenreAsync(TmdbGenre tmdbGenre, CancellationToken cancellationToken)
    {
        var name = TrimOrNull(tmdbGenre.Name);
        if (tmdbGenre.Id <= 0 || name is null)
        {
            return null;
        }

        var genre = dbContext.Genres.Local.FirstOrDefault(
            candidate => candidate.TmdbId == tmdbGenre.Id || string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

        genre ??= await dbContext.Genres.FirstOrDefaultAsync(
            candidate => candidate.TmdbId == tmdbGenre.Id || candidate.Name == name,
            cancellationToken);

        if (genre is not null)
        {
            if (genre.TmdbId == 0)
            {
                genre.TmdbId = tmdbGenre.Id;
            }

            return genre;
        }

        genre = new Genre
        {
            TmdbId = tmdbGenre.Id,
            Name = name,
            Slug = await EnsureUniqueGenreSlugAsync(Slugify(name), cancellationToken)
        };

        dbContext.Genres.Add(genre);
        return genre;
    }

    private async Task<Country?> GetOrCreateCountryAsync(TmdbCountry tmdbCountry, CancellationToken cancellationToken)
    {
        var name = TrimOrNull(tmdbCountry.Name);
        var code = TrimOrNull(tmdbCountry.Code)?.ToUpperInvariant();
        if (name is null && code is null)
        {
            return null;
        }

        var country = dbContext.Countries.Local.FirstOrDefault(
            candidate => (code != null && candidate.Code == code)
                || (name != null && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)));

        country ??= await dbContext.Countries.FirstOrDefaultAsync(
            candidate => (code != null && candidate.Code == code) || (name != null && candidate.Name == name),
            cancellationToken);

        if (country is not null)
        {
            country.Name = name ?? country.Name;
            if (code is not null && (country.Code is null || country.Code == code))
            {
                country.Code = code;
            }

            return country;
        }

        country = new Country
        {
            Name = name ?? code!,
            Code = code
        };
        dbContext.Countries.Add(country);
        return country;
    }

    private async Task<Language?> GetOrCreateLanguageAsync(TmdbLanguage tmdbLanguage, CancellationToken cancellationToken)
    {
        var name = TrimOrNull(tmdbLanguage.EnglishName) ?? TrimOrNull(tmdbLanguage.Name);
        var code = TrimOrNull(tmdbLanguage.Code)?.ToLowerInvariant();
        if (name is null && code is null)
        {
            return null;
        }

        var language = dbContext.Languages.Local.FirstOrDefault(
            candidate => (code != null && candidate.Code == code)
                || (name != null && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)));

        language ??= await dbContext.Languages.FirstOrDefaultAsync(
            candidate => (code != null && candidate.Code == code) || (name != null && candidate.Name == name),
            cancellationToken);

        if (language is not null)
        {
            language.Name = name ?? language.Name;
            if (code is not null && (language.Code is null || language.Code == code))
            {
                language.Code = code;
            }

            return language;
        }

        language = new Language
        {
            Name = name ?? code!,
            Code = code
        };
        dbContext.Languages.Add(language);
        return language;
    }

    private async Task<Person> GetOrCreatePersonAsync(
        int tmdbId,
        string fullName,
        string? profilePath,
        CancellationToken cancellationToken)
    {
        var name = fullName.Trim();
        var person = dbContext.Persons.Local.FirstOrDefault(
            candidate => candidate.TmdbId == tmdbId || string.Equals(candidate.FullName, name, StringComparison.OrdinalIgnoreCase));

        person ??= await dbContext.Persons.FirstOrDefaultAsync(
            candidate => candidate.TmdbId == tmdbId || candidate.FullName == name,
            cancellationToken);

        if (person is not null)
        {
            if (person.TmdbId == 0)
            {
                person.TmdbId = tmdbId;
            }

            person.PhotoUrl ??= BuildPosterUrl(profilePath);
            person.UpdatedAt = DateTimeOffset.UtcNow;
            return person;
        }

        person = new Person
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            FullName = name,
            PhotoUrl = BuildPosterUrl(profilePath),
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Persons.Add(person);
        return person;
    }

    private async Task<string> EnsureUniqueGenreSlugAsync(string slug, CancellationToken cancellationToken)
    {
        var uniqueSlug = slug;
        var suffix = 2;

        while (dbContext.Genres.Local.Any(genre => genre.Slug == uniqueSlug)
            || await dbContext.Genres.AnyAsync(genre => genre.Slug == uniqueSlug, cancellationToken))
        {
            uniqueSlug = $"{slug}-{suffix}";
            suffix++;
        }

        return uniqueSlug;
    }

    private static CreditType? MapCreditType(string? job, string? department)
    {
        if (string.Equals(job, "Director", StringComparison.OrdinalIgnoreCase))
        {
            return CreditType.Director;
        }

        if (string.Equals(job, "Writer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(job, "Screenplay", StringComparison.OrdinalIgnoreCase)
            || string.Equals(department, "Writing", StringComparison.OrdinalIgnoreCase))
        {
            return CreditType.Writer;
        }

        if (string.Equals(job, "Producer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(job, "Executive Producer", StringComparison.OrdinalIgnoreCase))
        {
            return CreditType.Producer;
        }

        return null;
    }

    private static string? BuildPosterUrl(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : $"{PosterBaseUrl}{path}";
    }

    private static string? BuildBackdropUrl(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : $"{BackdropBaseUrl}{path}";
    }

    private static decimal RoundRating(decimal value)
    {
        return Math.Round(value, 1, MidpointRounding.AwayFromZero);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string FormatException(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException concurrencyException)
        {
            var entries = string.Join(", ", concurrencyException.Entries.Select(entry => entry.Metadata.DisplayName()).Distinct());
            return string.IsNullOrWhiteSpace(entries)
                ? concurrencyException.Message
                : $"{concurrencyException.Message} Entries: {entries}";
        }

        var baseException = exception.GetBaseException();
        return baseException == exception
            ? exception.Message
            : $"{exception.Message} ({baseException.Message})";
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        var compact = SlugSeparatorsRegex().Replace(builder.ToString(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(compact) ? $"genre-{Guid.NewGuid():N}"[..20] : compact[..Math.Min(compact.Length, 100)];
    }

    [GeneratedRegex("-+")]
    private static partial Regex SlugSeparatorsRegex();

    private sealed class ImportAccumulator
    {
        private readonly List<string> errors = [];

        public int Fetched { get; set; }
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int ErrorsCount { get; private set; }

        public void AddError(string error)
        {
            ErrorsCount++;

            if (errors.Count < 20)
            {
                errors.Add(error);
            }
        }

        public ImportResultDto ToDto()
        {
            return new ImportResultDto(Fetched, Added, Updated, Skipped, ErrorsCount, errors);
        }
    }
}
