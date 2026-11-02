using CinemaCity.CatalogService.Repositories;
using CinemaCity.CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.CatalogService.Services;

internal static class MovieEndpointsWorkflow
{



    internal static async Task<IResult> GetGenresAsync(IRepository<Genre> genreRepository, CancellationToken cancellationToken)
    {
        var genres = await genreRepository.Query()
            .AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Select(genre => new GenreResponse(genre.Id, genre.Name, genre.Slug))
            .ToListAsync(cancellationToken);

        return Results.Ok(genres);
    }





    internal static async Task<IResult> GetMovieAsync(Guid id, IRepository<Movie> movieRepository, CancellationToken cancellationToken)
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

    internal static async Task<IResult> GetSimilarMoviesAsync(
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

    internal static async Task<IResult> CreateMovieAsync(
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

        return Results.Created($"/api/v1/catalog/items/{movie.Id}", new CreatedMovieDto(movie.Id));
    }

    internal static async Task<IResult> UpdateMovieAsync(
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

    internal static async Task<bool> MovieDuplicateExistsAsync(
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

    internal static async Task<IResult> DeleteMovieAsync(
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

    internal static async Task<IResult> RestoreMovieAsync(
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





    internal static async Task ApplyMovieRelationsAsync(
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

        if (movie.Genres.Count != genreIds.Length) throw new System.ComponentModel.DataAnnotations.ValidationException("One or more genre IDs do not exist.") { Data = { ["field"] = "GenreIds" } };
        if (movie.Countries.Count != countryIds.Length) throw new System.ComponentModel.DataAnnotations.ValidationException("One or more country IDs do not exist.") { Data = { ["field"] = "CountryIds" } };
        if (movie.Languages.Count != languageIds.Length) throw new System.ComponentModel.DataAnnotations.ValidationException("One or more language IDs do not exist.") { Data = { ["field"] = "LanguageIds" } };
        if (existingPersonIds.Count != personIds.Length) throw new System.ComponentModel.DataAnnotations.ValidationException("One or more person IDs do not exist.") { Data = { ["field"] = "Cast" } };
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

    internal static string? ValidateMovieRequest(MovieUpsertRequest request)
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

    internal static MovieCardResponse ToCardResponse(Movie movie)
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

    internal static MovieDetailsResponse ToDetailsResponse(Movie movie)
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

    internal static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}

