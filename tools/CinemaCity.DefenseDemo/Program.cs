using System.Net.Sockets;
using CinemaCity.Extraction.Tests;
using Npgsql;

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CINEMACITY_TEST_POSTGRES")))
    throw new InvalidOperationException("Set CINEMACITY_TEST_POSTGRES before starting. No working database is used.");
foreach (var port in new[] { 5000, 5001, 5002, 5003 })
{
    using var probe = new TcpClient();
    try { await probe.ConnectAsync("127.0.0.1", port); throw new InvalidOperationException($"Port {port} is already occupied. Stop the earlier stack first."); }
    catch (SocketException) { }
}
var system = new ExtractedSystem { StandardPorts = true };
try
{
    await system.InitializeAsync();
    void Info()
    {
        Console.WriteLine("\nDEMONSTRATION DATA ONLY — four isolated disposable databases. Original IDs preserved by the offline importer.");
        foreach (var pair in new[] { ("Source", system.SourceConnection), ("Auth", system.AuthConnection), ("Catalog", system.CatalogConnection), ("Community", system.CommunityConnection) })
            Console.WriteLine($"{pair.Item1}: {new NpgsqlConnectionStringBuilder(pair.Item2).Database}");
        Console.WriteLine($"Movie: {system.MovieId}\nSeries: {system.SeriesId}\nEpisode: {system.EpisodeId}\nOwner: {system.OwnerId}");
        Console.WriteLine($"Admin: {ExtractedSystem.AdminEmail}\nOwner login: {ExtractedSystem.OwnerEmail}\nDemo password: {ExtractedSystem.Password}");
        Console.WriteLine("Gateway http://localhost:5000/swagger; Auth :5001; Catalog :5002; Community :5003.");
        Console.WriteLine("Commands: info | stop catalog | start catalog | stop community | start community | logs catalog | logs community | exit");
    }
    var saved = new Dictionary<string, Dictionary<string, string>>();
    foreach (var name in new[] { "CinemaCity.CatalogService", "CinemaCity.CommunityService" })
        saved[name] = system.Apps.Single(app => app.Name == name).Process.StartInfo.Environment.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!);
    Info();
    var directory = Path.Combine(system.Root, ".local"); Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory, "defense-demo.json"), System.Text.Json.JsonSerializer.Serialize(new
    {
        movieId = system.MovieId, seriesId = system.SeriesId, ownerId = system.OwnerId,
        sourceDatabase = new NpgsqlConnectionStringBuilder(system.SourceConnection).Database,
        authDatabase = new NpgsqlConnectionStringBuilder(system.AuthConnection).Database,
        catalogDatabase = new NpgsqlConnectionStringBuilder(system.CatalogConnection).Database,
        communityDatabase = new NpgsqlConnectionStringBuilder(system.CommunityConnection).Database
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    while (true)
    {
        Console.Write("demo> ");
        var command = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (command is null or "exit") break;
        if (command == "info") { Info(); continue; }
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length != 2 || words[0] is not ("stop" or "start" or "logs") || words[1] is not ("catalog" or "community")) continue;
        var project = words[1] == "catalog" ? "CinemaCity.CatalogService" : "CinemaCity.CommunityService";
        var current = system.Apps.Last(app => app.Name == project);
        if (words[0] == "logs")
        {
            foreach (var line in current.Output.Split('\n').Where(line => line.Contains("GET /api/v1/catalog/items/") || line.Contains("GET /api/profile/movies/"))) Console.WriteLine(line);
        }
        else if (words[0] == "stop" && !current.Process.HasExited)
        { current.Process.Kill(entireProcessTree: true); await current.Process.WaitForExitAsync(); Console.WriteLine(project + " stopped"); }
        else if (words[0] == "start" && current.Process.HasExited)
        {
            var url = words[1] == "catalog" ? system.CatalogUrl : system.MonolithUrl;
            system.Start(project, url, saved[project]); await system.WaitReadyAsync(url); Console.WriteLine(project + " ready");
        }
    }
}
finally { await system.DisposeAsync(); Console.WriteLine("Demo stopped. Disposable databases removed."); }
