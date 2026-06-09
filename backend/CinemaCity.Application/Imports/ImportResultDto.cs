namespace CinemaCity.Application.Imports;

public sealed record ImportResultDto(
    int Fetched,
    int Added,
    int Updated,
    int Skipped,
    int ErrorsCount,
    IReadOnlyCollection<string> Errors);
