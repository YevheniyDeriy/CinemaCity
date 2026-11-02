using System.Text;
using System.Text.RegularExpressions;
using CinemaCity.Application.Imports;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CinemaCity.Infrastructure.Tmdb;

internal sealed partial class TmdbImportService(
    TmdbClient tmdbClient,
    IRepository<Movie> movieRepository,
    IRepository<MovieCast> movieCastRepository,
    IRepository<Genre> genreRepository,
    IRepository<Country> countryRepository,
    IRepository<Language> languageRepository,
    IRepository<Person> personRepository,
    IRepository<Season> seasonRepository,
    IRepository<Episode> episodeRepository,
    IRepository<Rating> ratingRepository,
    IUnitOfWork unitOfWork,
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

                await unitOfWork.SaveChangesAsync(cancellationToken);
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

                await unitOfWork.SaveChangesAsync(cancellationToken);
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
        await unitOfWork.SaveChangesAsync(cancellationToken);

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
            var videos = await tmdbClient.GetMovieVideosAsync(tmdbId, cancellationToken);
            var translations = await tmdbClient.GetMovieTranslationsAsync(tmdbId, cancellationToken);
            await UpsertMovieAsync(details, credits, videos, translations, result, cancellationToken);
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
            var videos = await tmdbClient.GetTvShowVideosAsync(tmdbId, cancellationToken);
            var translations = await tmdbClient.GetTvShowTranslationsAsync(tmdbId, cancellationToken);
            await UpsertTvShowAsync(details, credits, videos, translations, result, cancellationToken);
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
        TmdbVideosResponse videos,
        TmdbTranslationsResponse translations,
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

        var movie = await LoadMovieAsync(
            ContentType.Movie,
            details.Id,
            title,
            TrimOrNull(details.OriginalTitle),
            details.ReleaseDate?.Year ?? 0,
            cancellationToken);
        var isNew = movie is null;
        movie ??= new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = details.Id,
            ContentType = ContentType.Movie,
            CreatedAt = DateTimeOffset.UtcNow
        };

        movie.TmdbId = details.Id;
        movie.Title = title;
        movie.OriginalTitle = TrimOrNull(details.OriginalTitle);
        movie.ReleaseYear = details.ReleaseDate?.Year ?? 0;
        movie.Duration = details.Runtime;
        movie.Description = PickOverview(translations, details.Overview);
        movie.Status = TrimOrNull(details.Status);
        movie.Tagline = TrimOrNull(details.Tagline);
        movie.FirstAirDate = null;
        movie.LastAirDate = null;
        movie.SeasonsCount = null;
        movie.EpisodesCount = null;
        movie.PosterUrl = BuildPosterUrl(details.PosterPath);
        movie.BackdropUrl = BuildBackdropUrl(details.BackdropPath);
        movie.TrailerYoutubeId = PickYoutubeTrailerId(videos) ?? movie.TrailerYoutubeId;
        movie.AverageRating = RoundRating(details.VoteAverage);
        movie.RatingsCount = details.VoteCount;
        movie.IsDeleted = false;
        movie.DeletedAt = null;
        movie.UpdatedAt = isNew ? null : DateTimeOffset.UtcNow;

        await SyncDictionariesAsync(movie, details.Genres, details.ProductionCountries, details.SpokenLanguages, cancellationToken);
        await SyncCreditsAsync(movie, credits, isNew, cancellationToken);

        if (isNew)
        {
            movieRepository.Add(movie);
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
        TmdbVideosResponse videos,
        TmdbTranslationsResponse translations,
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

        var series = await LoadMovieAsync(
            ContentType.Series,
            details.Id,
            title,
            TrimOrNull(details.OriginalName),
            details.FirstAirDate?.Year ?? 0,
            cancellationToken);
        var isNew = series is null;
        series ??= new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = details.Id,
            ContentType = ContentType.Series,
            CreatedAt = DateTimeOffset.UtcNow
        };

        series.TmdbId = details.Id;
        series.Title = title;
        series.OriginalTitle = TrimOrNull(details.OriginalName);
        series.ReleaseYear = details.FirstAirDate?.Year ?? 0;
        series.Duration = null;
        series.Description = PickOverview(translations, details.Overview);
        series.Status = TrimOrNull(details.Status);
        series.Tagline = TrimOrNull(details.Tagline);
        series.FirstAirDate = details.FirstAirDate;
        series.LastAirDate = details.LastAirDate;
        series.SeasonsCount = details.NumberOfSeasons;
        series.EpisodesCount = details.NumberOfEpisodes;
        series.PosterUrl = BuildPosterUrl(details.PosterPath);
        series.BackdropUrl = BuildBackdropUrl(details.BackdropPath);
        series.TrailerYoutubeId = PickYoutubeTrailerId(videos) ?? series.TrailerYoutubeId;
        series.AverageRating = RoundRating(details.VoteAverage);
        series.RatingsCount = details.VoteCount;
        series.IsDeleted = false;
        series.DeletedAt = null;
        series.UpdatedAt = isNew ? null : DateTimeOffset.UtcNow;

        await SyncDictionariesAsync(series, details.Genres, details.ProductionCountries, details.SpokenLanguages, cancellationToken);
        await SyncCreditsAsync(series, credits, isNew, cancellationToken);
        await SyncTvSeasonsAsync(series, details, cancellationToken);

        if (isNew)
        {
            movieRepository.Add(series);
            result.Added++;
        }
        else
        {
            result.Updated++;
        }
    }

    private async Task SyncTvSeasonsAsync(Movie series, TmdbTvShowDetails details, CancellationToken cancellationToken)
    {
        var seasonSummaries = (details.Seasons ?? [])
            .Where(season => season.SeasonNumber > 0)
            .OrderBy(season => season.SeasonNumber)
            .ToArray();

        foreach (var summary in seasonSummaries)
        {
            TmdbSeasonDetails? seasonDetails;
            TmdbSeasonDetails? ukrainianSeasonDetails = null;

            try
            {
                seasonDetails = await tmdbClient.GetTvSeasonDetailsAsync(details.Id, summary.SeasonNumber, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "TMDB TV show {TmdbId} season {SeasonNumber} failed.",
                    details.Id,
                    summary.SeasonNumber);
                continue;
            }

            try
            {
                ukrainianSeasonDetails = await tmdbClient.GetTvSeasonDetailsAsync(details.Id, summary.SeasonNumber, cancellationToken, "uk-UA");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "TMDB TV show {TmdbId} season {SeasonNumber} Ukrainian localization failed.",
                    details.Id,
                    summary.SeasonNumber);
            }

            var season = await GetOrCreateSeasonAsync(series, summary, seasonDetails, ukrainianSeasonDetails, cancellationToken);
            await SyncEpisodesAsync(season, seasonDetails, ukrainianSeasonDetails, cancellationToken);
        }

        if (seasonSummaries.Length > 0)
        {
            series.SeasonsCount = seasonSummaries.Length;
            series.EpisodesCount = seasonSummaries.Sum(season => season.EpisodeCount ?? 0);
        }
    }

    private async Task<Season> GetOrCreateSeasonAsync(
        Movie series,
        TmdbSeasonSummary summary,
        TmdbSeasonDetails details,
        TmdbSeasonDetails? ukrainianDetails,
        CancellationToken cancellationToken)
    {
        var season = seasonRepository.Local.FirstOrDefault(candidate =>
            candidate.SeriesId == series.Id && candidate.SeasonNumber == summary.SeasonNumber);

        season ??= await seasonRepository.Query()
            .FirstOrDefaultAsync(
                candidate => candidate.SeriesId == series.Id && candidate.SeasonNumber == summary.SeasonNumber,
                cancellationToken);

        var title = TrimOrNull(details.Name) ?? TrimOrNull(summary.Name);
        var overview = PickOverview(ukrainianDetails?.Overview, details.Overview) ?? TrimOrNull(summary.Overview);

        if (season is null)
        {
            season = new Season
            {
                Id = Guid.NewGuid(),
                SeriesId = series.Id,
                SeasonNumber = summary.SeasonNumber
            };
            seasonRepository.Add(season);
        }

        season.Title = title;
        season.Description = overview;
        season.ReleaseYear = details.AirDate?.Year ?? summary.AirDate?.Year;

        return season;
    }

    private async Task SyncEpisodesAsync(
        Season season,
        TmdbSeasonDetails seasonDetails,
        TmdbSeasonDetails? ukrainianSeasonDetails,
        CancellationToken cancellationToken)
    {
        var ukrainianEpisodesByNumber = (ukrainianSeasonDetails?.Episodes ?? [])
            .Where(episode => episode.EpisodeNumber > 0)
            .ToDictionary(episode => episode.EpisodeNumber);
        var existingEpisodesByNumber = await LoadSeasonEpisodesAsync(season.Id, cancellationToken);
        var ratedEpisodeIds = existingEpisodesByNumber.Count == 0
            ? []
            : await LoadRatedEpisodeIdsAsync(existingEpisodesByNumber.Values.Select(episode => episode.Id), cancellationToken);

        foreach (var tmdbEpisode in (seasonDetails.Episodes ?? [])
            .Where(episode => episode.EpisodeNumber > 0)
            .OrderBy(episode => episode.EpisodeNumber))
        {
            var title = TrimOrNull(tmdbEpisode.Name);
            if (title is null)
            {
                continue;
            }

            existingEpisodesByNumber.TryGetValue(tmdbEpisode.EpisodeNumber, out var episode);
            var isNew = episode is null;

            if (episode is null)
            {
                episode = new Episode
                {
                    Id = Guid.NewGuid(),
                    SeasonId = season.Id,
                    EpisodeNumber = tmdbEpisode.EpisodeNumber
                };
                episodeRepository.Add(episode);
                existingEpisodesByNumber[episode.EpisodeNumber] = episode;
            }

            episode.Title = title;
            ukrainianEpisodesByNumber.TryGetValue(tmdbEpisode.EpisodeNumber, out var ukrainianEpisode);
            episode.Description = PickOverview(ukrainianEpisode?.Overview, tmdbEpisode.Overview);
            episode.AirDate = tmdbEpisode.AirDate;
            episode.Duration = tmdbEpisode.Runtime;

            if (isNew || !ratedEpisodeIds.Contains(episode.Id))
            {
                episode.AverageRating = RoundRating(tmdbEpisode.VoteAverage);
                episode.RatingsCount = tmdbEpisode.VoteCount;
            }
        }
    }

    private async Task<Dictionary<int, Episode>> LoadSeasonEpisodesAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var episodes = episodeRepository.Local
            .Where(episode => episode.SeasonId == seasonId)
            .ToList();
        var trackedEpisodeIds = episodes.Select(episode => episode.Id).ToHashSet();
        var persistedEpisodes = await episodeRepository.Query()
            .Where(episode => episode.SeasonId == seasonId && !trackedEpisodeIds.Contains(episode.Id))
            .ToListAsync(cancellationToken);

        episodes.AddRange(persistedEpisodes);
        return episodes
            .GroupBy(episode => episode.EpisodeNumber)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private async Task<HashSet<Guid>> LoadRatedEpisodeIdsAsync(IEnumerable<Guid> episodeIds, CancellationToken cancellationToken)
    {
        var ids = episodeIds.ToHashSet();
        if (ids.Count == 0)
        {
            return [];
        }

        var ratedEpisodeIds = await ratingRepository.Query()
            .Where(rating => rating.EpisodeId.HasValue && ids.Contains(rating.EpisodeId.Value))
            .Select(rating => rating.EpisodeId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ratedEpisodeIds.ToHashSet();
    }

    private async Task<Movie?> LoadMovieAsync(
        ContentType contentType,
        int tmdbId,
        string title,
        string? originalTitle,
        int releaseYear,
        CancellationToken cancellationToken)
    {
        var local = movieRepository.Local.FirstOrDefault(movie =>
            movie.ContentType == contentType
            && (movie.TmdbId == tmdbId
                || IsSameTitle(movie, title, originalTitle, releaseYear)));

        if (local is not null)
        {
            return local;
        }

        return await movieRepository.Query()
            .Include(movie => movie.Genres)
            .Include(movie => movie.Countries)
            .Include(movie => movie.Languages)
            .FirstOrDefaultAsync(
                movie => movie.ContentType == contentType
                    && (movie.TmdbId == tmdbId
                        || (movie.ReleaseYear == releaseYear
                            && (movie.Title.ToLower() == title.ToLower()
                                || (originalTitle != null
                                    && movie.OriginalTitle != null
                                    && movie.OriginalTitle.ToLower() == originalTitle.ToLower())))),
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

    private async Task SyncCreditsAsync(Movie movie, TmdbCredits credits, bool isNew, CancellationToken cancellationToken)
    {
        if (!isNew)
        {
            await movieCastRepository.Query()
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

            movieCastRepository.Add(new MovieCast
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

            movieCastRepository.Add(new MovieCast
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

        var genre = genreRepository.Local.FirstOrDefault(
            candidate => candidate.TmdbId == tmdbGenre.Id || string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

        genre ??= await genreRepository.Query().FirstOrDefaultAsync(
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

        genreRepository.Add(genre);
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

        var country = countryRepository.Local.FirstOrDefault(
            candidate => (code != null && candidate.Code == code)
                || (name != null && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)));

        country ??= await countryRepository.Query().FirstOrDefaultAsync(
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
        countryRepository.Add(country);
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

        var language = languageRepository.Local.FirstOrDefault(
            candidate => (code != null && candidate.Code == code)
                || (name != null && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)));

        language ??= await languageRepository.Query().FirstOrDefaultAsync(
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
        languageRepository.Add(language);
        return language;
    }

    private async Task<Person> GetOrCreatePersonAsync(
        int tmdbId,
        string fullName,
        string? profilePath,
        CancellationToken cancellationToken)
    {
        var name = fullName.Trim();
        var person = personRepository.Local.FirstOrDefault(
            candidate => candidate.TmdbId == tmdbId || string.Equals(candidate.FullName, name, StringComparison.OrdinalIgnoreCase));

        person ??= await personRepository.Query().FirstOrDefaultAsync(
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
        personRepository.Add(person);
        return person;
    }

    private async Task<string> EnsureUniqueGenreSlugAsync(string slug, CancellationToken cancellationToken)
    {
        var uniqueSlug = slug;
        var suffix = 2;

        while (genreRepository.Local.Any(genre => genre.Slug == uniqueSlug)
            || await genreRepository.Query().AnyAsync(genre => genre.Slug == uniqueSlug, cancellationToken))
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

    private static string? PickYoutubeTrailerId(TmdbVideosResponse videos)
    {
        return (videos.Results ?? [])
            .Where(video =>
                !string.IsNullOrWhiteSpace(video.Key)
                && string.Equals(video.Site, "YouTube", StringComparison.OrdinalIgnoreCase)
                && string.Equals(video.Type, "Trailer", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(video => video.Official)
            .ThenByDescending(video => video.PublishedAt)
            .Select(video => video.Key!.Trim())
            .FirstOrDefault();
    }

    private static string? PickOverview(TmdbTranslationsResponse translations, string? fallbackOverview)
    {
        return PickUkrainianOverview(translations) ?? TrimOrNull(fallbackOverview);
    }

    private static string? PickOverview(string? localizedOverview, string? fallbackOverview)
    {
        return PickUkrainianOverview(localizedOverview) ?? TrimOrNull(fallbackOverview);
    }

    private static string? PickUkrainianOverview(TmdbTranslationsResponse translations)
    {
        return (translations.Translations ?? [])
            .Where(translation => string.Equals(translation.LanguageCode, "uk", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(translation => string.Equals(translation.CountryCode, "UA", StringComparison.OrdinalIgnoreCase))
            .Select(translation => PickUkrainianOverview(translation.Data?.Overview))
            .FirstOrDefault(overview => overview is not null);
    }

    private static string? PickUkrainianOverview(string? overview)
    {
        var trimmed = TrimOrNull(overview);
        return trimmed is not null && ContainsCyrillic(trimmed) ? trimmed : null;
    }

    private static bool ContainsCyrillic(string value)
    {
        return value.Any(character => character is >= '\u0400' and <= '\u04FF');
    }

    private static bool IsSameTitle(Movie movie, string title, string? originalTitle, int releaseYear)
    {
        return movie.ReleaseYear == releaseYear
            && (string.Equals(movie.Title, title, StringComparison.OrdinalIgnoreCase)
                || (originalTitle is not null
                    && movie.OriginalTitle is not null
                    && string.Equals(movie.OriginalTitle, originalTitle, StringComparison.OrdinalIgnoreCase)));
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
