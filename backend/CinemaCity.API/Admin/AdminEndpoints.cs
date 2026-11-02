using System.Text;
using System.Text.RegularExpressions;
using CinemaCity.Application.Persistence;
using CinemaCity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CinemaCity.API.Admin;

public static partial class AdminEndpoints
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private static readonly Dictionary<string, string> ExtensionByContentType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/api/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin");

        admin.MapGet("/genres", GetGenresAsync)
            .WithName("AdminGetGenres");

        admin.MapPost("/genres", CreateGenreAsync)
            .WithName("AdminCreateGenre");

        admin.MapPut("/genres/{id:int}", UpdateGenreAsync)
            .WithName("AdminUpdateGenre");

        admin.MapDelete("/genres/{id:int}", DeleteGenreAsync)
            .WithName("AdminDeleteGenre");

        admin.MapGet("/persons", GetPersonsAsync)
            .WithName("AdminGetPersons");

        admin.MapPost("/persons", CreatePersonAsync)
            .WithName("AdminCreatePerson");

        admin.MapPut("/persons/{id:guid}", UpdatePersonAsync)
            .WithName("AdminUpdatePerson");

        admin.MapDelete("/persons/{id:guid}", DeletePersonAsync)
            .WithName("AdminDeletePerson");

        admin.MapPost("/movies/{movieId:guid}/poster", UploadMoviePosterAsync)
            .DisableAntiforgery()
            .WithName("AdminUploadMoviePoster");

        admin.MapPost("/persons/{personId:guid}/photo", UploadPersonPhotoAsync)
            .DisableAntiforgery()
            .WithName("AdminUploadPersonPhoto");

        admin.MapPost("/uploads/poster", (HttpRequest request, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
                UploadImageAsync(request, environment, "posters", cancellationToken))
            .DisableAntiforgery()
            .WithName("AdminUploadPoster");

        admin.MapPost("/uploads/photo", (HttpRequest request, IWebHostEnvironment environment, CancellationToken cancellationToken) =>
                UploadImageAsync(request, environment, "photos", cancellationToken))
            .DisableAntiforgery()
            .WithName("AdminUploadPhoto");

        MapTmdbImportEndpoints(admin);

        return routes;
    }

    private static async Task<IResult> GetGenresAsync(IRepository<Genre> genreRepository, CancellationToken cancellationToken)
    {
        var genres = await genreRepository.Query()
            .AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Select(genre => new AdminGenreResponse(
                genre.Id,
                genre.Name,
                genre.Slug,
                genre.Movies.Count))
            .ToListAsync(cancellationToken);

        return Results.Ok(genres);
    }

    private static async Task<IResult> CreateGenreAsync(
        GenreUpsertRequest request,
        IRepository<Genre> genreRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateGenreRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var slug = await EnsureUniqueSlugAsync(
            genreRepository,
            NormalizeSlug(request.Slug, request.Name),
            excludedGenreId: null,
            cancellationToken);

        var genre = new Genre
        {
            Name = request.Name.Trim(),
            Slug = slug
        };

        genreRepository.Add(genre);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/genres/{genre.Id}", new AdminGenreResponse(genre.Id, genre.Name, genre.Slug, 0));
    }

    private static async Task<IResult> UpdateGenreAsync(
        int id,
        GenreUpsertRequest request,
        IRepository<Genre> genreRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidateGenreRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var genre = await genreRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (genre is null)
        {
            return Results.NotFound(new { Message = "Genre was not found." });
        }

        genre.Name = request.Name.Trim();
        genre.Slug = await EnsureUniqueSlugAsync(
            genreRepository,
            NormalizeSlug(request.Slug, request.Name),
            genre.Id,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteGenreAsync(
        int id,
        IRepository<Genre> genreRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var genre = await genreRepository.Query()
            .Include(candidate => candidate.Movies)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (genre is null)
        {
            return Results.NotFound(new { Message = "Genre was not found." });
        }

        genreRepository.Remove(genre);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetPersonsAsync(
        IRepository<Person> personRepository,
        string? q,
        CancellationToken cancellationToken)
    {
        var query = personRepository.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = q.Trim().ToLower();
            query = query.Where(person =>
                person.FullName.ToLower().Contains(search)
                || (person.BirthPlace != null && person.BirthPlace.ToLower().Contains(search)));
        }

        var persons = await query
            .OrderBy(person => person.FullName)
            .Take(100)
            .Select(person => new AdminPersonResponse(
                person.Id,
                person.FullName,
                person.BirthDate,
                person.BirthPlace,
                person.Bio,
                person.PhotoUrl,
                person.Credits.Count))
            .ToListAsync(cancellationToken);

        return Results.Ok(persons);
    }

    private static async Task<IResult> CreatePersonAsync(
        PersonUpsertRequest request,
        IRepository<Person> personRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidatePersonRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var person = new Person
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            BirthDate = request.BirthDate,
            BirthPlace = TrimOrNull(request.BirthPlace),
            Bio = TrimOrNull(request.Bio),
            PhotoUrl = TrimOrNull(request.PhotoUrl),
            CreatedAt = DateTimeOffset.UtcNow
        };

        personRepository.Add(person);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/persons/{person.Id}", ToPersonResponse(person, 0));
    }

    private static async Task<IResult> UpdatePersonAsync(
        Guid id,
        PersonUpsertRequest request,
        IRepository<Person> personRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var validation = ValidatePersonRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var person = await personRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (person is null)
        {
            return Results.NotFound(new { Message = "Person was not found." });
        }

        person.FullName = request.FullName.Trim();
        person.BirthDate = request.BirthDate;
        person.BirthPlace = TrimOrNull(request.BirthPlace);
        person.Bio = TrimOrNull(request.Bio);
        person.PhotoUrl = TrimOrNull(request.PhotoUrl);
        person.UpdatedAt = DateTimeOffset.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeletePersonAsync(
        Guid id,
        IRepository<Person> personRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var person = await personRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (person is null)
        {
            return Results.NotFound(new { Message = "Person was not found." });
        }

        personRepository.Remove(person);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UploadMoviePosterAsync(
        Guid movieId,
        HttpRequest request,
        IWebHostEnvironment environment,
        IRepository<Movie> movieRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var movie = await movieRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == movieId, cancellationToken);
        if (movie is null)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var fileRead = await ReadUploadFileAsync(request, cancellationToken);
        if (fileRead.Result is not null)
        {
            return fileRead.Result;
        }

        var upload = await SaveImageAsync(
            request,
            environment,
            fileRead.File!,
            "posters",
            $"movie_poster_{movieId:N}",
            cancellationToken);

        if (upload.Result is not null)
        {
            return upload.Result;
        }

        movie.PosterUrl = upload.Response!.Path;
        movie.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Ok(upload.Response);
    }

    private static async Task<IResult> UploadPersonPhotoAsync(
        Guid personId,
        HttpRequest request,
        IWebHostEnvironment environment,
        IRepository<Person> personRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var person = await personRepository.Query().FirstOrDefaultAsync(candidate => candidate.Id == personId, cancellationToken);
        if (person is null)
        {
            return Results.NotFound(new { Message = "Person was not found." });
        }

        var fileRead = await ReadUploadFileAsync(request, cancellationToken);
        if (fileRead.Result is not null)
        {
            return fileRead.Result;
        }

        var upload = await SaveImageAsync(
            request,
            environment,
            fileRead.File!,
            "photos",
            $"person_photo_{personId:N}",
            cancellationToken);

        if (upload.Result is not null)
        {
            return upload.Result;
        }

        person.PhotoUrl = upload.Response!.Path;
        person.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Results.Ok(upload.Response);
    }

    private static async Task<IResult> UploadImageAsync(
        HttpRequest request,
        IWebHostEnvironment environment,
        string bucket,
        CancellationToken cancellationToken)
    {
        var fileRead = await ReadUploadFileAsync(request, cancellationToken);
        if (fileRead.Result is not null)
        {
            return fileRead.Result;
        }

        var upload = await SaveImageAsync(
            request,
            environment,
            fileRead.File!,
            bucket,
            $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}",
            cancellationToken);

        return upload.Result ?? Results.Ok(upload.Response);
    }

    private static async Task<(IResult? Result, IFormFile? File)> ReadUploadFileAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return (Results.BadRequest(new { Message = "Upload must be sent as multipart/form-data." }), null);
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

        if (file is null)
        {
            return (Results.BadRequest(new { Message = "Form field 'file' is required." }), null);
        }

        return (null, file);
    }

    private static async Task<(IResult? Result, UploadResponse? Response)> SaveImageAsync(
        HttpRequest request,
        IWebHostEnvironment environment,
        IFormFile file,
        string bucket,
        string fileNameWithoutExtension,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return (Results.BadRequest(new { Message = "File is empty." }), null);
        }

        if (file.Length > MaxUploadBytes)
        {
            return (Results.BadRequest(new { Message = "File is too large. Maximum size is 5 MB." }), null);
        }

        if (!AllowedImageContentTypes.Contains(file.ContentType))
        {
            return (Results.BadRequest(new { Message = "Only JPG, PNG, WEBP and GIF images are allowed." }), null);
        }

        var uploadRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", bucket);
        Directory.CreateDirectory(uploadRoot);

        var extension = ExtensionByContentType[file.ContentType];
        var fileName = $"{fileNameWithoutExtension}{extension}";
        var filePath = Path.Combine(uploadRoot, fileName);

        foreach (var staleFile in Directory.EnumerateFiles(uploadRoot, $"{fileNameWithoutExtension}.*"))
        {
            File.Delete(staleFile);
        }

        await using (var stream = File.Create(filePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var path = $"/uploads/{bucket}/{fileName}";
        var url = $"{request.Scheme}://{request.Host}{path}";

        return (null, new UploadResponse(path, url));
    }

    private static AdminPersonResponse ToPersonResponse(Person person, int creditsCount)
    {
        return new AdminPersonResponse(
            person.Id,
            person.FullName,
            person.BirthDate,
            person.BirthPlace,
            person.Bio,
            person.PhotoUrl,
            creditsCount);
    }

    private static string? ValidateGenreRequest(GenreUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
        {
            return "Genre name is required and must be at most 100 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.Slug) && request.Slug.Length > 100)
        {
            return "Genre slug must be at most 100 characters.";
        }

        return null;
    }

    private static string? ValidatePersonRequest(PersonUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 255)
        {
            return "Person name is required and must be at most 255 characters.";
        }

        if (!string.IsNullOrWhiteSpace(request.BirthPlace) && request.BirthPlace.Length > 255)
        {
            return "Birth place must be at most 255 characters.";
        }

        if (request.BirthDate is not null && request.BirthDate > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return "Birth date cannot be in the future.";
        }

        return null;
    }

    private static async Task<string> EnsureUniqueSlugAsync(
        IRepository<Genre> genreRepository,
        string slug,
        int? excludedGenreId,
        CancellationToken cancellationToken)
    {
        var uniqueSlug = slug;
        var suffix = 2;

        while (await genreRepository.Query().AnyAsync(
            genre => genre.Slug == uniqueSlug && (excludedGenreId == null || genre.Id != excludedGenreId),
            cancellationToken))
        {
            uniqueSlug = $"{slug}-{suffix}";
            suffix++;
        }

        return uniqueSlug;
    }

    private static string NormalizeSlug(string? slug, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(slug) ? fallback : slug;
        var normalized = source.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
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

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [GeneratedRegex("-+")]
    private static partial Regex SlugSeparatorsRegex();
}

public sealed record AdminGenreResponse(int Id, string Name, string Slug, int MoviesCount);

public sealed record GenreUpsertRequest(string Name, string? Slug);

public sealed record AdminPersonResponse(
    Guid Id,
    string FullName,
    DateOnly? BirthDate,
    string? BirthPlace,
    string? Bio,
    string? PhotoUrl,
    int CreditsCount);

public sealed record PersonUpsertRequest(
    string FullName,
    DateOnly? BirthDate,
    string? BirthPlace,
    string? Bio,
    string? PhotoUrl);

public sealed record UploadResponse(string Path, string Url);
