namespace CinemaCity.Infrastructure.Tmdb;

public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";
    public string ApiToken { get; set; } = string.Empty;
}
