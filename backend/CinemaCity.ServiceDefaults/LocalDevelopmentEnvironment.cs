using System.Text.RegularExpressions;

namespace CinemaCity.ServiceDefaults;

/// <summary>Loads the ignored repository .env file for plain local dotnet run launches.</summary>
public static class LocalDevelopmentEnvironment
{
    public static void Load(string application)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            return;

        var values = ReadRepositoryEnvironmentFile();
        if (values.Count == 0) return;

        SetIfMissing("Jwt__Secret", Get(values, "JWT_SECRET"));
        SetIfMissing("Services__InternalKey", Get(values, "SERVICE_INTERNAL_KEY"));
        SetIfMissing("Google__ClientId", Get(values, "GOOGLE_CLIENT_ID"));
        SetIfMissing("Google__ClientSecret", Get(values, "GOOGLE_CLIENT_SECRET"));
        SetIfMissing("Tmdb__ApiToken", Get(values, "TMDB_API_TOKEN"));

        var connectionKey = application switch
        {
            "CinemaCity.AuthService" => "CINEMACITY_AUTH_CONNECTION",
            "CinemaCity.CatalogService" => "CINEMACITY_CATALOG_CONNECTION",
            "CinemaCity.API" => "CINEMACITY_MONOLITH_CONNECTION",
            _ => null
        };
        if (connectionKey is not null)
            SetIfMissing("ConnectionStrings__DefaultConnection", Get(values, connectionKey));

        if (application == "CinemaCity.API")
            SetIfMissing("Services__Enabled", "true");
    }

    private static Dictionary<string, string> ReadRepositoryEnvironmentFile()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName, ".env");
            if (!File.Exists(file)) continue;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadLines(file))
            {
                var match = Regex.Match(line, @"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$");
                if (!match.Success) continue;

                var value = match.Groups[2].Value.Trim();
                if (value.Length >= 2 &&
                    ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                    value = value[1..^1];
                values[match.Groups[1].Value] = value;
            }
            return values;
        }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static void SetIfMissing(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)) && !string.IsNullOrWhiteSpace(value))
            Environment.SetEnvironmentVariable(key, value);
    }
}
