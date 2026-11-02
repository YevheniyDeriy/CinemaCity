using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Movies;

public static class MovieEndpoints
{
    private const int MaxPageSize = 50;

    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/genres", GetGenresAsync)
            .WithName("GetGenres");

        routes.MapGet("/api/movies", GetMoviesAsync)
            .WithName("GetMovies");

        routes.MapGet("/api/movies/search", SearchMoviesAsync)
            .WithName("SearchMovies");

        routes.MapGet("/api/movies/{id:guid}", GetMovieAsync)
            .WithName("GetMovie");

        routes.MapGet("/api/movies/{id:guid}/similar", GetSimilarMoviesAsync)
            .WithName("GetSimilarMovies");

        var admin = routes.MapGroup("/api/admin/movies")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin Movies");

        admin.MapPost("/", CreateMovieAsync)
            .WithName("CreateMovie");

        admin.MapPut("/{id:guid}", UpdateMovieAsync)
            .WithName("UpdateMovie");

        admin.MapDelete("/{id:guid}", DeleteMovieAsync)
            .WithName("DeleteMovie");

        admin.MapPut("/{id:guid}/restore", RestoreMovieAsync)
            .WithName("RestoreMovie");

        return routes;
    }

    private static async Task<IResult> GetGenresAsync(IRepository<Genre> genreRepository, CancellationToken cancellationToken)
    {
        var genres = await genreRepository.Query()
            .AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Select(genre => new GenreResponse(genre.Id, genre.Name, genre.Slug))
            .ToListAsync(cancellationToken);

        return Results.Ok(genres);
    }

    private static async Task<IResult> GetMoviesAsync(
        IRepository<Movie> movieRepository,
        string? q,
        string? genre,
        int? yearFrom,
        int? yearTo,
        decimal? ratingMin,
        string? contentType,
        string? sort,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = BuildMovieQuery(movieRepository, q, genre, yearFrom, yearTo, ratingMin, contentType);
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var total = await query.CountAsync(cancellationToken);
        var items = await ApplyMovieSort(query, sort)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(movie => ToCardResponse(movie))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResult<MovieCardResponse>(items, normalizedPage, normalizedPageSize, total));
    }

    private static Task<IResult> SearchMoviesAsync(
        IRepository<Movie> movieRepository,
        string? q,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return GetMoviesAsync(movieRepository, q, null, null, null, null, null, "rating", page, pageSize, cancellationToken);
    }

    private static async Task<IResult> GetMovieAsync(Guid id, IRepository<Movie> movieRepository, CancellationToken cancellationToken)
    {
        var movie = await movieRepository.Query()
            .AsNoTracking()
            .Include(candidate => candidate.Genres)
            .Include(candidate => candidate.Countries)
            .Include(candidate => candidate.Languages)
            .Include(candidate => candidate.Cast)
                .ThenInclude(cast => cast.Person)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && !candidate.IsDeleted, cancellationToken);

        if (movie is null)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        return Results.Ok(ToDetailsResponse(movie));
    }

    private static async Task<IResult> GetSimilarMoviesAsync(
        Guid id,
        IRepository<Movie> movieRepository,
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var genreIds = await movieRepository.Query()
            .AsNoTracking()
            .Where(movie => movie.Id == id && !movie.IsDeleted)
            .SelectMany(movie => movie.Genres.Select(genre => genre.Id))
            .ToListAsync(cancellationToken);

        if (genreIds.Count == 0)
        {
            return Results.Ok(Array.Empty<MovieCardResponse>());
        }

        var normalizedLimit = Math.Clamp(limit, 1, 20);
        var movies = await movieRepository.Query()
            .AsNoTracking()
            .Where(movie => movie.Id != id && !movie.IsDeleted)
            .Where(movie => movie.Genres.Any(genre => genreIds.Contains(genre.Id)))
            .OrderByDescending(movie => movie.Genres.Count(genre => genreIds.Contains(genre.Id)))
            .ThenByDescending(movie => movie.AverageRating)
            .ThenByDescending(movie => movie.RatingsCount)
            .Take(normalizedLimit)
            .Select(movie => ToCardResponse(movie))
            .ToListAsync(cancellationToken);

        return Results.Ok(movies);
    }

    private static async Task<IResult> CreateMovieAsync(
        MovieUpsertRequest request,
        IRepository<Movie> movieRepository,
        IRepository<Genre> genreRepository,
        IRepository<Country> countryRepository,
        IRepository<Language> languageRepository,
        IRepository<Person> personRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMovieRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        if (await MovieDuplicateExistsAsync(movieRepository, request, excludedMovieId: null, cancellationToken))
        {
            return Results.Conflict(new { Message = "Movie or series already exists in the catalog." });
        }

        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            OriginalTitle = TrimOrNull(request.OriginalTitle),
            ContentType = request.ContentType,
            ReleaseYear = request.ReleaseYear,
            Duration = request.Duration,
            Description = TrimOrNull(request.Description),
            PosterUrl = TrimOrNull(request.PosterUrl),
            BackdropUrl = TrimOrNull(request.BackdropUrl),
            TrailerYoutubeId = TrimOrNull(request.TrailerYoutubeId),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await ApplyMovieRelationsAsync(movie, request, genreRepository, countryRepository, languageRepository, personRepository, cancellationToken);
        movieRepository.Add(movie);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/movies/{movie.Id}", new { movie.Id });
    }

    private static async Task<IResult> UpdateMovieAsync(
        Guid id,
        MovieUpsertRequest request,
        IRepository<Movie> movieRepository,
        IRepository<MovieCast> movieCastRepository,
        IRepository<Genre> genreRepository,
        IRepository<Country> countryRepository,
        IRepository<Language> languageRepository,
        IRepository<Person> personRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateMovieRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var movie = await movieRepository.Query()
            .Include(candidate => candidate.Genres)
            .Include(candidate => candidate.Countries)
            .Include(candidate => candidate.Languages)
            .Include(candidate => candidate.Cast)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (movie is null)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        if (await MovieDuplicateExistsAsync(movieRepository, request, id, cancellationToken))
        {
            return Results.Conflict(new { Message = "Movie or series already exists in the catalog." });
        }

        movie.Title = request.Title.Trim();
        movie.OriginalTitle = TrimOrNull(request.OriginalTitle);
        movie.ContentType = request.ContentType;
        movie.ReleaseYear = request.ReleaseYear;
        movie.Duration = request.Duration;
        movie.Description = TrimOrNull(request.Description);
        movie.PosterUrl = TrimOrNull(request.PosterUrl);
        movie.BackdropUrl = TrimOrNull(request.BackdropUrl);
        movie.TrailerYoutubeId = TrimOrNull(request.TrailerYoutubeId);
        movie.UpdatedAt = DateTimeOffset.UtcNow;

        movieCastRepository.RemoveRange(movie.Cast);
        movie.Genres.Clear();
        movie.Countries.Clear();
        movie.Languages.Clear();

        await ApplyMovieRelationsAsync(movie, request, genreRepository, countryRepository, languageRepository, personRepository, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<bool> MovieDuplicateExistsAsync(
        IRepository<Movie> movieRepository,
        MovieUpsertRequest request,
        Guid? excludedMovieId,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim().ToLower();
        var originalTitle = TrimOrNull(request.OriginalTitle)?.ToLower();

        return await movieRepository.Query().AnyAsync(movie =>
            !movie.IsDeleted
            && movie.ContentType == request.ContentType
            && movie.ReleaseYear == request.ReleaseYear
            && (excludedMovieId == null || movie.Id != excludedMovieId)
            && (movie.Title.ToLower() == title
                || (originalTitle != null && movie.OriginalTitle != null && movie.OriginalTitle.ToLower() == originalTitle)),
            cancellationToken);
    }

    private static async Task<IResult> DeleteMovieAsync(
        Guid id,
        IRepository<Movie> movieRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var movie = await movieRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (movie is null)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        movie.IsDeleted = true;
        movie.DeletedAt = DateTimeOffset.UtcNow;
        movie.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> RestoreMovieAsync(
        Guid id,
        IRepository<Movie> movieRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var movie = await movieRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (movie is null)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        movie.IsDeleted = false;
        movie.DeletedAt = null;
        movie.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static IQueryable<Movie> BuildMovieQuery(
        IRepository<Movie> movieRepository,
        string? q,
        string? genre,
        int? yearFrom,
        int? yearTo,
        decimal? ratingMin,
        string? contentType)
    {
        var query = movieRepository.Query()
            .AsNoTracking()
            .Where(movie => !movie.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            query = query.Where(movie =>
                movie.Title.ToLower().Contains(search)
                || (movie.OriginalTitle != null && movie.OriginalTitle.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            var normalizedGenre = genre.Trim().ToLower();
            query = query.Where(movie => movie.Genres.Any(candidate =>
                candidate.Slug.ToLower() == normalizedGenre || candidate.Name.ToLower() == normalizedGenre));
        }

        if (yearFrom is not null)
        {
            query = query.Where(movie => movie.ReleaseYear >= yearFrom);
        }

        if (yearTo is not null)
        {
            query = query.Where(movie => movie.ReleaseYear <= yearTo);
        }

        if (ratingMin is not null)
        {
            query = query.Where(movie => movie.AverageRating >= ratingMin);
        }

        if (!string.IsNullOrWhiteSpace(contentType)
            && Enum.TryParse<ContentType>(contentType, ignoreCase: true, out var parsedContentType))
        {
            query = query.Where(movie => movie.ContentType == parsedContentType);
        }

        return query;
    }

    private static IOrderedQueryable<Movie> ApplyMovieSort(IQueryable<Movie> query, string? sort)
    {
        return sort?.Trim().ToLower() switch
        {
            "title" => query.OrderBy(movie => movie.Title),
            "year" => query.OrderByDescending(movie => movie.ReleaseYear).ThenBy(movie => movie.Title),
            "newest" => query.OrderByDescending(movie => movie.CreatedAt),
            "rating" or _ => query
                .OrderByDescending(movie => movie.AverageRating)
                .ThenByDescending(movie => movie.RatingsCount)
                .ThenBy(movie => movie.Title)
        };
    }

    private static async Task ApplyMovieRelationsAsync(
        Movie movie,
        MovieUpsertRequest request,
        IRepository<Genre> genreRepository,
        IRepository<Country> countryRepository,
        IRepository<Language> languageRepository,
        IRepository<Person> personRepository,
        CancellationToken cancellationToken)
    {
        var genreIds = request.GenreIds.Distinct().ToArray();
        var countryIds = request.CountryIds.Distinct().ToArray();
        var languageIds = request.LanguageIds.Distinct().ToArray();
        var personIds = request.Cast.Select(cast => cast.PersonId).Distinct().ToArray();

        movie.Genres = await genreRepository.Query()
            .Where(genre => genreIds.Contains(genre.Id))
            .ToListAsync(cancellationToken);

        movie.Countries = await countryRepository.Query()
            .Where(country => countryIds.Contains(country.Id))
            .ToListAsync(cancellationToken);

        movie.Languages = await languageRepository.Query()
            .Where(language => languageIds.Contains(language.Id))
            .ToListAsync(cancellationToken);

        var existingPersonIds = await personRepository.Query()
            .Where(person => personIds.Contains(person.Id))
            .Select(person => person.Id)
            .ToListAsync(cancellationToken);

        movie.Cast = request.Cast
            .Where(cast => existingPersonIds.Contains(cast.PersonId))
            .Select(cast => new MovieCast
            {
                Id = Guid.NewGuid(),
                MovieId = movie.Id,
                PersonId = cast.PersonId,
                CreditType = cast.CreditType,
                RoleName = TrimOrNull(cast.RoleName),
                SortOrder = cast.SortOrder
            })
            .ToList();
    }

    private static string? ValidateMovieRequest(MovieUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 255)
        {
            return "Movie title is required and must be at most 255 characters.";
        }

        if (request.ReleaseYear is < 1888 or > 2200)
        {
            return "Release year is invalid.";
        }

        if (request.Duration is not null and <= 0)
        {
            return "Duration must be greater than zero.";
        }

        if (request.Cast.Any(cast => cast.SortOrder < 0))
        {
            return "Cast sort order cannot be negative.";
        }

        return null;
    }

    private static MovieCardResponse ToCardResponse(Movie movie)
    {
        return new MovieCardResponse(
            movie.Id,
            movie.Title,
            movie.OriginalTitle,
            movie.ContentType,
            movie.ReleaseYear,
            movie.PosterUrl,
            movie.BackdropUrl,
            movie.AverageRating,
            movie.RatingsCount,
            movie.Genres
                .OrderBy(genre => genre.Name)
                .Select(genre => new GenreResponse(genre.Id, genre.Name, genre.Slug))
                .ToArray());
    }

    private static MovieDetailsResponse ToDetailsResponse(Movie movie)
    {
        return new MovieDetailsResponse(
            movie.Id,
            movie.Title,
            movie.OriginalTitle,
            movie.ContentType,
            movie.ReleaseYear,
            movie.Duration,
            movie.Description,
            movie.PosterUrl,
            movie.BackdropUrl,
            movie.TrailerYoutubeId,
            movie.AverageRating,
            movie.RatingsCount,
            movie.Genres.OrderBy(genre => genre.Name).Select(genre => new GenreResponse(genre.Id, genre.Name, genre.Slug)).ToArray(),
            movie.Countries.OrderBy(country => country.Name).Select(country => new DictionaryResponse(country.Id, country.Name, country.Code)).ToArray(),
            movie.Languages.OrderBy(language => language.Name).Select(language => new DictionaryResponse(language.Id, language.Name, language.Code)).ToArray(),
            movie.Cast
                .OrderBy(cast => cast.SortOrder)
                .ThenBy(cast => cast.Person.FullName)
                .Select(cast => new CastResponse(
                    cast.Id,
                    cast.PersonId,
                    cast.Person.FullName,
                    cast.Person.PhotoUrl,
                    cast.CreditType,
                    cast.RoleName,
                    cast.SortOrder))
                .ToArray());
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total);

public sealed record GenreResponse(int Id, string Name, string Slug);

public sealed record DictionaryResponse(int Id, string Name, string? Code);

public sealed record MovieCardResponse(
    Guid Id,
    string Title,
    string? OriginalTitle,
    ContentType ContentType,
    int ReleaseYear,
    string? PosterUrl,
    string? BackdropUrl,
    decimal AverageRating,
    int RatingsCount,
    IReadOnlyCollection<GenreResponse> Genres);

public sealed record MovieDetailsResponse(
    Guid Id,
    string Title,
    string? OriginalTitle,
    ContentType ContentType,
    int ReleaseYear,
    int? Duration,
    string? Description,
    string? PosterUrl,
    string? BackdropUrl,
    string? TrailerYoutubeId,
    decimal AverageRating,
    int RatingsCount,
    IReadOnlyCollection<GenreResponse> Genres,
    IReadOnlyCollection<DictionaryResponse> Countries,
    IReadOnlyCollection<DictionaryResponse> Languages,
    IReadOnlyCollection<CastResponse> Cast);

public sealed record CastResponse(
    Guid Id,
    Guid PersonId,
    string FullName,
    string? PhotoUrl,
    CreditType CreditType,
    string? RoleName,
    int SortOrder);

public sealed record MovieUpsertRequest(
    string Title,
    string? OriginalTitle,
    ContentType ContentType,
    int ReleaseYear,
    int? Duration,
    string? Description,
    string? PosterUrl,
    string? BackdropUrl,
    string? TrailerYoutubeId,
    int[] GenreIds,
    int[] CountryIds,
    int[] LanguageIds,
    MovieCastUpsertRequest[] Cast);

public sealed record MovieCastUpsertRequest(
    Guid PersonId,
    CreditType CreditType,
    string? RoleName,
    int SortOrder);
