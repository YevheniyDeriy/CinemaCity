using System.Text.Json.Serialization;

namespace CinemaCity.CatalogService.Tmdb;

internal sealed record TmdbPagedResponse<T>(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("results")] IReadOnlyCollection<T>? Results);

internal sealed record TmdbListItem(
    [property: JsonPropertyName("id")] int Id);

internal sealed record TmdbMovieDetails(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("original_title")] string? OriginalTitle,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("release_date")] DateOnly? ReleaseDate,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("backdrop_path")] string? BackdropPath,
    [property: JsonPropertyName("vote_average")] decimal VoteAverage,
    [property: JsonPropertyName("vote_count")] int VoteCount,
    [property: JsonPropertyName("runtime")] int? Runtime,
    [property: JsonPropertyName("genres")] IReadOnlyCollection<TmdbGenre>? Genres,
    [property: JsonPropertyName("production_countries")] IReadOnlyCollection<TmdbCountry>? ProductionCountries,
    [property: JsonPropertyName("spoken_languages")] IReadOnlyCollection<TmdbLanguage>? SpokenLanguages,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("tagline")] string? Tagline);

internal sealed record TmdbTvShowDetails(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("original_name")] string? OriginalName,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("first_air_date")] DateOnly? FirstAirDate,
    [property: JsonPropertyName("last_air_date")] DateOnly? LastAirDate,
    [property: JsonPropertyName("poster_path")] string? PosterPath,
    [property: JsonPropertyName("backdrop_path")] string? BackdropPath,
    [property: JsonPropertyName("vote_average")] decimal VoteAverage,
    [property: JsonPropertyName("vote_count")] int VoteCount,
    [property: JsonPropertyName("number_of_seasons")] int? NumberOfSeasons,
    [property: JsonPropertyName("number_of_episodes")] int? NumberOfEpisodes,
    [property: JsonPropertyName("seasons")] IReadOnlyCollection<TmdbSeasonSummary>? Seasons,
    [property: JsonPropertyName("genres")] IReadOnlyCollection<TmdbGenre>? Genres,
    [property: JsonPropertyName("production_countries")] IReadOnlyCollection<TmdbCountry>? ProductionCountries,
    [property: JsonPropertyName("spoken_languages")] IReadOnlyCollection<TmdbLanguage>? SpokenLanguages,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("tagline")] string? Tagline);

internal sealed record TmdbSeasonSummary(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("season_number")] int SeasonNumber,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("air_date")] DateOnly? AirDate,
    [property: JsonPropertyName("episode_count")] int? EpisodeCount);

internal sealed record TmdbSeasonDetails(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("season_number")] int SeasonNumber,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("air_date")] DateOnly? AirDate,
    [property: JsonPropertyName("episodes")] IReadOnlyCollection<TmdbEpisodeDetails>? Episodes);

internal sealed record TmdbEpisodeDetails(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("episode_number")] int EpisodeNumber,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("overview")] string? Overview,
    [property: JsonPropertyName("air_date")] DateOnly? AirDate,
    [property: JsonPropertyName("runtime")] int? Runtime,
    [property: JsonPropertyName("vote_average")] decimal VoteAverage,
    [property: JsonPropertyName("vote_count")] int VoteCount);

internal sealed record TmdbGenre(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record TmdbCountry(
    [property: JsonPropertyName("iso_3166_1")] string? Code,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record TmdbLanguage(
    [property: JsonPropertyName("iso_639_1")] string? Code,
    [property: JsonPropertyName("english_name")] string? EnglishName,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record TmdbCredits(
    [property: JsonPropertyName("cast")] IReadOnlyCollection<TmdbCastMember>? Cast,
    [property: JsonPropertyName("crew")] IReadOnlyCollection<TmdbCrewMember>? Crew);

internal sealed record TmdbVideosResponse(
    [property: JsonPropertyName("results")] IReadOnlyCollection<TmdbVideo>? Results);

internal sealed record TmdbVideo(
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("site")] string? Site,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("official")] bool Official,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt);

internal sealed record TmdbTranslationsResponse(
    [property: JsonPropertyName("translations")] IReadOnlyCollection<TmdbTranslation>? Translations);

internal sealed record TmdbTranslation(
    [property: JsonPropertyName("iso_3166_1")] string? CountryCode,
    [property: JsonPropertyName("iso_639_1")] string? LanguageCode,
    [property: JsonPropertyName("data")] TmdbTranslationData? Data);

internal sealed record TmdbTranslationData(
    [property: JsonPropertyName("overview")] string? Overview);

internal sealed record TmdbCastMember(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("profile_path")] string? ProfilePath,
    [property: JsonPropertyName("character")] string? Character,
    [property: JsonPropertyName("order")] int? Order);

internal sealed record TmdbCrewMember(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("profile_path")] string? ProfilePath,
    [property: JsonPropertyName("job")] string? Job,
    [property: JsonPropertyName("department")] string? Department);
