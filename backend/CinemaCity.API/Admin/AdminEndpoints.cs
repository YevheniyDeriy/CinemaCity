using System.Text;
using System.Text.RegularExpressions;
using CinemaCity.Domain.Entities;
using CinemaCity.Infrastructure.Data;
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

        admin.MapPost("/uploads/poster", (HttpRequest request, IWebHostEnvironment environment, IFormFile file, CancellationToken cancellationToken) =>
                UploadImageAsync(request, environment, file, "posters", cancellationToken))
            .DisableAntiforgery()
            .WithName("AdminUploadPoster");

        admin.MapPost("/uploads/photo", (HttpRequest request, IWebHostEnvironment environment, IFormFile file, CancellationToken cancellationToken) =>
                UploadImageAsync(request, environment, file, "photos", cancellationToken))
            .DisableAntiforgery()
            .WithName("AdminUploadPhoto");

        MapTmdbImportEndpoints(admin);

        return routes;
    }

    private static async Task<IResult> GetGenresAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var genres = await dbContext.Genres
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
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateGenreRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var slug = await EnsureUniqueSlugAsync(
            dbContext,
            NormalizeSlug(request.Slug, request.Name),
            excludedGenreId: null,
            cancellationToken);

        var genre = new Genre
        {
            Name = request.Name.Trim(),
            Slug = slug
        };

        dbContext.Genres.Add(genre);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/genres/{genre.Id}", new AdminGenreResponse(genre.Id, genre.Name, genre.Slug, 0));
    }

    private static async Task<IResult> UpdateGenreAsync(
        int id,
        GenreUpsertRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidateGenreRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var genre = await dbContext.Genres.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (genre is null)
        {
            return Results.NotFound(new { Message = "Genre was not found." });
        }

        genre.Name = request.Name.Trim();
        genre.Slug = await EnsureUniqueSlugAsync(
            dbContext,
            NormalizeSlug(request.Slug, request.Name),
            genre.Id,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteGenreAsync(int id, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var genre = await dbContext.Genres
            .Include(candidate => candidate.Movies)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (genre is null)
        {
            return Results.NotFound(new { Message = "Genre was not found." });
        }

        dbContext.Genres.Remove(genre);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetPersonsAsync(
        AppDbContext dbContext,
        string? q,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Persons.AsNoTracking();

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
        AppDbContext dbContext,
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

        dbContext.Persons.Add(person);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admin/persons/{person.Id}", ToPersonResponse(person, 0));
    }

    private static async Task<IResult> UpdatePersonAsync(
        Guid id,
        PersonUpsertRequest request,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var validation = ValidatePersonRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { Message = validation });
        }

        var person = await dbContext.Persons.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeletePersonAsync(Guid id, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var person = await dbContext.Persons.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (person is null)
        {
            return Results.NotFound(new { Message = "Person was not found." });
        }

        dbContext.Persons.Remove(person);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UploadMoviePosterAsync(
        Guid movieId,
        HttpRequest request,
        IWebHostEnvironment environment,
        AppDbContext dbContext,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var movie = await dbContext.Movies.FirstOrDefaultAsync(candidate => candidate.Id == movieId, cancellationToken);
        if (movie is null)
        {
            return Results.NotFound(new { Message = "Movie was not found." });
        }

        var upload = await SaveImageAsync(
            request,
            environment,
            file,
            "posters",
            $"movie_poster_{movieId:N}",
            cancellationToken);

        if (upload.Result is not null)
        {
            return upload.Result;
        }

        movie.PosterUrl = upload.Response!.Path;
        movie.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(upload.Response);
    }

    private static async Task<IResult> UploadPersonPhotoAsync(
        Guid personId,
        HttpRequest request,
        IWebHostEnvironment environment,
        AppDbContext dbContext,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var person = await dbContext.Persons.FirstOrDefaultAsync(candidate => candidate.Id == personId, cancellationToken);
        if (person is null)
        {
            return Results.NotFound(new { Message = "Person was not found." });
        }

        var upload = await SaveImageAsync(
            request,
            environment,
            file,
            "photos",
            $"person_photo_{personId:N}",
            cancellationToken);

        if (upload.Result is not null)
        {
            return upload.Result;
        }

        person.PhotoUrl = upload.Response!.Path;
        person.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(upload.Response);
    }

    private static async Task<IResult> UploadImageAsync(
        HttpRequest request,
        IWebHostEnvironment environment,
        IFormFile file,
        string bucket,
        CancellationToken cancellationToken)
    {
        var upload = await SaveImageAsync(
            request,
            environment,
            file,
            bucket,
            $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}",
            cancellationToken);

        return upload.Result ?? Results.Ok(upload.Response);
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

        return null;
    }

    private static async Task<string> EnsureUniqueSlugAsync(
        AppDbContext dbContext,
        string slug,
        int? excludedGenreId,
        CancellationToken cancellationToken)
    {
        var uniqueSlug = slug;
        var suffix = 2;

        while (await dbContext.Genres.AnyAsync(
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
