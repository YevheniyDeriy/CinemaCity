using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text;
using CinemaCity.Domain.Entities;
using CinemaCity.Domain.Enums;
using CinemaCity.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CinemaCity.Extraction.Tests;

/// <summary>A real four-process stack and three disposable databases. Never targets the user's CinemaCity data.</summary>
public sealed class ExtractedSystem : IAsyncLifetime
{
    public bool StandardPorts { get; init; }
    public const string Password = "OriginalCinemaCity123!";
    public const string AdminEmail = "original.admin@cinemacity.tests.local";
    public const string OwnerEmail = "original.owner@cinemacity.tests.local";
    public readonly Guid MovieId = Guid.NewGuid(), SeriesId = Guid.NewGuid(), EpisodeId = Guid.NewGuid(), OwnerId = Guid.NewGuid();
    public string JwtSecret { get; } = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
    public string InternalKey { get; } = Guid.NewGuid().ToString("N");
    public HttpClient Gateway { get; private set; } = null!;
    public string SourceConnection { get; private set; } = null!;
    public string CommunityConnection { get; private set; } = null!;
    public string AuthConnection { get; private set; } = null!;
    public string CatalogConnection { get; private set; } = null!;
    public Uri AuthUrl { get; private set; } = null!;
    public Uri CatalogUrl { get; private set; } = null!;
    public Uri MonolithUrl { get; private set; } = null!;
    public string Root { get; } = FindRoot();
    public List<RunningApp> Apps { get; } = [];
    private readonly List<string> _databases = [];

    public string AdditionalDatabase(string kind)
    {
        var name = $"cinemacity_e2e_{kind}_{Guid.NewGuid():N}";
        _databases.Add(name);
        return new NpgsqlConnectionStringBuilder(SourceConnection) { Database = name }.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CINEMACITY_TEST_POSTGRES") ?? "Host=127.0.0.1;Port=55432;Username=postgres;Password=postgres");
        string NewDatabase(string kind) { var name = $"cinemacity_e2e_{kind}_{Guid.NewGuid():N}"; _databases.Add(name); return new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = name }.ConnectionString; }
        SourceConnection = NewDatabase("source"); AuthConnection = NewDatabase("auth"); CatalogConnection = NewDatabase("catalog"); CommunityConnection = NewDatabase("community");
        try
        {
            await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(SourceConnection).Options))
            {
                await db.Database.EnsureCreatedAsync();
                var admin = new User { Id = Guid.NewGuid(), Username = "original_admin", Email = AdminEmail, Role = UserRole.Admin };
                var owner = new User { Id = OwnerId, Username = "original_owner", Email = OwnerEmail };
                var hasher = new PasswordHasher<User>();
                admin.PasswordHash = hasher.HashPassword(admin, Password); owner.PasswordHash = hasher.HashPassword(owner, Password);
                var genre = new Genre { Id = 1, Name = "Original drama", Slug = "original-drama" };
                var person = new Person { Id = Guid.NewGuid(), FullName = "Original CinemaCity actor" };
                var movie = new Movie { Id = MovieId, Title = "Original CinemaCity film", ContentType = ContentType.Movie, ReleaseYear = 2024, Description = "Existing monolith content", Genres = [genre], Cast = [new MovieCast { Id = Guid.NewGuid(), Person = person, CreditType = CreditType.Actor, RoleName = "Original role" }] };
                var series = new Movie { Id = SeriesId, Title = "Original CinemaCity series", ContentType = ContentType.Series, ReleaseYear = 2025, Genres = [genre], Seasons = [new Season { Id = Guid.NewGuid(), SeasonNumber = 1, Title = "Original season", Episodes = [new Episode { Id = EpisodeId, EpisodeNumber = 1, Title = "Original episode" }] }] };
                db.Users.AddRange(admin, owner); db.Movies.AddRange(movie, series);
                db.Reviews.Add(new Review { Id = Guid.NewGuid(), UserId = OwnerId, MovieId = MovieId, Content = "Original monolith review", Status = ReviewStatus.Approved });
                await db.SaveChangesAsync();
            }
            var migration = Start("CinemaCity.DataMigration", null, new() { ["CINEMACITY_SOURCE_CONNECTION"] = SourceConnection, ["CINEMACITY_AUTH_CONNECTION"] = AuthConnection, ["CINEMACITY_CATALOG_CONNECTION"] = CatalogConnection, ["CINEMACITY_COMMUNITY_CONNECTION"] = CommunityConnection });
            await migration.Process.WaitForExitAsync();
            Assert.True(migration.Process.ExitCode == 0, migration.Output);
            AuthUrl = StandardPorts ? new Uri("http://127.0.0.1:5001") : Url(); CatalogUrl = StandardPorts ? new Uri("http://127.0.0.1:5002") : Url(); MonolithUrl = StandardPorts ? new Uri("http://127.0.0.1:5003") : Url(); var gatewayUrl = StandardPorts ? new Uri("http://127.0.0.1:5000") : Url();
            var common = new Dictionary<string, string>
            {
                ["Jwt__Secret"] = JwtSecret, ["Jwt__Issuer"] = "CinemaCity.Tests", ["Jwt__Audience"] = "CinemaCity.Tests",
                ["Services__InternalKey"] = InternalKey, ["Services__AuthUrl"] = AuthUrl.ToString(), ["Services__CatalogUrl"] = CatalogUrl.ToString(), ["Services__MonolithUrl"] = MonolithUrl.ToString(), ["Services__CommunityUrl"] = MonolithUrl.ToString(),
                ["Database__MigrateOnStartup"] = "false", ["Seed__Enabled"] = "false", ["Google__ClientId"] = "", ["Google__ClientSecret"] = ""
            };
            var auth = new Dictionary<string, string>(common) { ["ConnectionStrings__DefaultConnection"] = AuthConnection };
            var catalog = new Dictionary<string, string>(common) { ["ConnectionStrings__DefaultConnection"] = CatalogConnection, ["Logging__LogLevel__CinemaCity.CatalogService.Repositories.CatalogRepository"] = "Debug" };
            var monolith = new Dictionary<string, string>(common) { ["ConnectionStrings__DefaultConnection"] = CommunityConnection, ["Services__Enabled"] = "true", ["Database__MigrateOnStartup"] = StandardPorts ? "true" : "false" };
            Start("CinemaCity.AuthService", AuthUrl, auth); Start("CinemaCity.CatalogService", CatalogUrl, catalog); Start("CinemaCity.CommunityService", MonolithUrl, monolith);
            await Task.WhenAll(WaitReadyAsync(AuthUrl), WaitReadyAsync(CatalogUrl), WaitReadyAsync(MonolithUrl));
            Start("CinemaCity.ApiGateway", gatewayUrl, new Dictionary<string, string>(common) { ["Gateway__Mode"] = "Services" });
            await WaitReadyAsync(gatewayUrl);
            Gateway = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = gatewayUrl, Timeout = TimeSpan.FromSeconds(30) };
        }
        catch { await DisposeAsync(); throw; }
    }

    public RunningApp Start(string project, Uri? url, Dictionary<string, string> environment)
    {
        if (project == "CinemaCity.ApiGateway")
        {
            if (!StandardPorts) { environment.TryAdd("Gateway__Limits__login", "1000"); environment.TryAdd("Gateway__TimeoutSeconds", "15"); } // Shared legacy fixture logs in repeatedly; rate policies have isolated Gateway.Tests.
            environment.TryAdd("Jwt__Secret", JwtSecret); environment.TryAdd("Jwt__Issuer", "CinemaCity.Tests"); environment.TryAdd("Jwt__Audience", "CinemaCity.Tests");
        }
        var folder = Path.Combine(Root, project == "CinemaCity.DataMigration" ? "tools" : "backend", project);
        var configuration = Environment.GetEnvironmentVariable("CINEMACITY_TEST_CONFIGURATION") ?? "Debug";
        var dll = Path.Combine(folder, ".build", "bin", configuration, "net10.0", project + ".dll");
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(dll);
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        info.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        if (url is not null) info.Environment["ASPNETCORE_URLS"] = url.ToString();
        foreach (var (name, value) in environment) info.Environment[name] = value;
        var app = new RunningApp(project, new Process { StartInfo = info });
        app.Process.OutputDataReceived += (_, args) => app.Append(args.Data);
        app.Process.ErrorDataReceived += (_, args) => app.Append(args.Data);
        app.Process.Start(); app.Process.BeginOutputReadLine(); app.Process.BeginErrorReadLine(); Apps.Add(app);
        return app;
    }

    public async Task WaitReadyAsync(Uri url, int expected = 200)
    {
        using var client = new HttpClient { BaseAddress = url, Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 200; attempt++)
        {
            try { using var response = await client.GetAsync("/health"); if ((int)response.StatusCode == expected) return; }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(100);
        }
        throw new InvalidOperationException($"Application at {url} did not become ready.\n" + string.Join("\n", Apps.Select(app => app.Name + ":\n" + app.Output)));
    }

    public async Task DisposeAsync()
    {
        Gateway?.Dispose();
        foreach (var app in Apps.AsEnumerable().Reverse())
        {
            if (!app.Process.HasExited) { app.Process.Kill(entireProcessTree: true); await app.Process.WaitForExitAsync(); }
            var logs = Path.Combine(Root, ".logs", "extraction-tests"); Directory.CreateDirectory(logs);
            await File.WriteAllTextAsync(Path.Combine(logs, app.Name + "-" + app.Process.Id + ".log"), app.Output);
            app.Process.Dispose();
        }
        Apps.Clear();
        NpgsqlConnection.ClearAllPools();
        var baseConnection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CINEMACITY_TEST_POSTGRES") ?? "Host=127.0.0.1;Port=55432;Username=postgres;Password=postgres") { Database = "postgres" };
        await using var connection = new NpgsqlConnection(baseConnection.ConnectionString);
        await connection.OpenAsync();
        foreach (var name in _databases)
        {
            if (!name.StartsWith("cinemacity_e2e_", StringComparison.Ordinal)) throw new InvalidOperationException("Refusing to drop a non-test database.");
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
        _databases.Clear();
    }

    public static Uri Url() { using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); return new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}"); }
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CinemaCity.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot find CinemaCity.slnx.");
    }
}

public sealed class RunningApp(string name, Process process)
{
    private readonly StringBuilder _output = new();
    public string Name { get; } = name;
    public Process Process { get; } = process;
    public string Output { get { lock (_output) return _output.ToString(); } }
    public void Append(string? line) { if (line is not null) lock (_output) _output.AppendLine(line); }
}
