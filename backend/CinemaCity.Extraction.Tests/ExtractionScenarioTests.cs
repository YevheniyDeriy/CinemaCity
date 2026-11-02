using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace CinemaCity.Extraction.Tests;

public sealed class ExtractionScenarioTests(ExtractedSystem system) : IClassFixture<ExtractedSystem>
{
    [Fact]
    public async Task MyRatings_ArePrivateAndCancellationRemovesTheOperation()
    {
        var admin = await LoginAsync(ExtractedSystem.AdminEmail);
        var owner = await LoginAsync(ExtractedSystem.OwnerEmail);
        var movie = await SendAsync(HttpMethod.Post, "/api/v1/catalog/items", MoviePayload("Private ratings " + Guid.NewGuid()), 201, admin);
        var movieId = movie.GetProperty("id").GetGuid();
        var rating = await SendAsync(HttpMethod.Post, "/api/ratings", new { movieId, score = 8, userId = Guid.NewGuid() }, 201, owner);
        var id = rating.GetProperty("id").GetGuid();
        var mine = await SendAsync(HttpMethod.Get, "/api/ratings", token: owner);
        Assert.Contains(mine.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id && item.GetProperty("status").GetString() == "Active");
        var others = await SendAsync(HttpMethod.Get, "/api/ratings", token: admin);
        Assert.DoesNotContain(others.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
        await SendAsync(HttpMethod.Get, "/api/ratings", expected: 401);
        var name = "privacy_" + Guid.NewGuid().ToString("N")[..8];
        var otherSession = await SendAsync(HttpMethod.Post, "/api/auth/register", new { username = name, email = name + "@tests.local", password = ExtractedSystem.Password, confirmPassword = ExtractedSystem.Password }, 201);
        var other = otherSession.GetProperty("accessToken").GetString()!;
        await SendAsync(HttpMethod.Put, "/api/ratings/" + id, new { score = 9 }, expected: 404, token: other);
        await SendAsync(HttpMethod.Delete, "/api/ratings/" + id, expected: 404, token: other);
        await SendAsync(HttpMethod.Delete, "/api/ratings/" + id, expected: 204, token: owner);
        var removed = await SendAsync(HttpMethod.Get, "/api/ratings", token: owner);
        Assert.DoesNotContain(removed.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task CatalogOutage_Returns503WithoutStoppingCommunity()
    {
        var url = ExtractedSystem.Url();
        system.Start("CinemaCity.CommunityService", url, new()
        {
            ["ConnectionStrings__DefaultConnection"] = system.CommunityConnection,
            ["Database__MigrateOnStartup"] = "false", ["Jwt__Secret"] = system.JwtSecret,
            ["Jwt__Issuer"] = "CinemaCity.Tests", ["Jwt__Audience"] = "CinemaCity.Tests",
            ["Services__AuthUrl"] = system.AuthUrl.ToString(), ["Services__CatalogUrl"] = ExtractedSystem.Url().ToString(),
            ["Services__InternalKey"] = system.InternalKey, ["Google__ClientId"] = ""
        });
        await system.WaitReadyAsync(url);
        using var client = new HttpClient { BaseAddress = url };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ratings") { Content = JsonContent.Create(new { movieId = system.MovieId, score = 5 }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(ExtractedSystem.OwnerEmail));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(503, problem.GetProperty("status").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task CommunityBaseline_EightErrorContractsMatchTheUnchangedMonolith()
    {
        var oldUrl = ExtractedSystem.Url();
        system.Start("CinemaCity.API", oldUrl, new()
        {
            ["ConnectionStrings__DefaultConnection"] = system.SourceConnection, ["Services__Enabled"] = "false",
            ["Database__MigrateOnStartup"] = "false", ["Jwt__Secret"] = system.JwtSecret,
            ["Jwt__Issuer"] = "CinemaCity.Tests", ["Jwt__Audience"] = "CinemaCity.Tests",
            ["Services__AuthUrl"] = system.AuthUrl.ToString(), ["Services__CatalogUrl"] = system.CatalogUrl.ToString(),
            ["Services__InternalKey"] = system.InternalKey, ["Google__ClientId"] = ""
        });
        await system.WaitReadyAsync(oldUrl);
        using var old = new HttpClient { BaseAddress = oldUrl };
        var token = await LoginAsync(ExtractedSystem.OwnerEmail);
        var missing = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var cases = new (string Name, HttpMethod Method, string Path, object Body, bool Auth, int Status)[]
        {
            ("score-zero", HttpMethod.Post, "/api/ratings", new { movieId = system.MovieId, score = 0 }, true, 400),
            ("score-eleven", HttpMethod.Post, "/api/ratings", new { movieId = system.MovieId, score = 11 }, true, 400),
            ("no-target", HttpMethod.Post, "/api/ratings", new { score = 5 }, true, 400),
            ("two-targets", HttpMethod.Post, "/api/ratings", new { movieId = system.MovieId, episodeId = system.EpisodeId, score = 5 }, true, 400),
            ("missing-movie", HttpMethod.Post, "/api/ratings", new { movieId = missing, score = 5 }, true, 404),
            ("anonymous-create", HttpMethod.Post, "/api/ratings", new { movieId = system.MovieId, score = 5 }, false, 401),
            ("missing-update", HttpMethod.Put, "/api/ratings/" + missing, new { score = 5 }, true, 404),
            ("missing-delete", HttpMethod.Delete, "/api/ratings/" + missing, new { }, true, 404)
        };
        var directory = System.IO.Path.Combine(system.Root, "tests", "baseline"); Directory.CreateDirectory(directory);
        foreach (var item in cases)
        {
            async Task<(int Status, string Body)> Call(HttpClient client)
            {
                using var request = new HttpRequestMessage(item.Method, item.Path);
                if (item.Method != HttpMethod.Delete) request.Content = JsonContent.Create(item.Body);
                if (item.Auth) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request);
                return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
            var before = await Call(old); var after = await Call(system.Gateway);
            Assert.Equal(item.Status, before.Status);
            if (before.Status == 401) { Assert.Equal(401, after.Status); Assert.Equal(401, JsonSerializer.Deserialize<JsonElement>(after.Body).GetProperty("status").GetInt32()); }
            else Assert.Equal(before, after);
            await File.WriteAllTextAsync(System.IO.Path.Combine(directory, item.Name + ".json"), JsonSerializer.Serialize(new { source = "unchanged working-tree monolith", method = item.Method.Method, path = item.Path, status = before.Status, body = before.Body }, new JsonSerializerOptions { WriteIndented = true }));
        }

        async Task<(int Status, string Body, string? Location)> Operation(HttpClient client, HttpMethod method, string path, object? payload)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (payload is not null) request.Content = JsonContent.Create(payload);
            using var response = await client.SendAsync(request);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Location?.ToString());
        }
        string Normalize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            foreach (var field in new[] { "id", "createdAt", "updatedAt" })
                if (node.ContainsKey(field)) node[field] = "<generated>";
            return node.ToJsonString();
        }
        var createdBefore = await Operation(old, HttpMethod.Post, "/api/ratings", new { movieId = system.MovieId, score = 6 });
        var createdAfter = await Operation(system.Gateway, HttpMethod.Post, "/api/ratings", new { movieId = system.MovieId, score = 6 });
        Assert.Equal(201, createdBefore.Status); Assert.Equal(201, createdAfter.Status);
        Assert.Equal(Normalize(createdBefore.Body), Normalize(createdAfter.Body));
        var beforeId = JsonSerializer.Deserialize<JsonElement>(createdBefore.Body).GetProperty("id").GetGuid();
        var afterId = JsonSerializer.Deserialize<JsonElement>(createdAfter.Body).GetProperty("id").GetGuid();
        Assert.Equal("/api/ratings/" + beforeId, createdBefore.Location);
        Assert.Equal("/api/ratings/" + afterId, createdAfter.Location);
        await File.WriteAllTextAsync(System.IO.Path.Combine(directory, "valid-create.json"), JsonSerializer.Serialize(createdBefore, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
        foreach (var step in new[] { (Name: "valid-update", Method: HttpMethod.Put, Payload: (object?)new { score = 8 }, Status: 200), (Name: "valid-delete", Method: HttpMethod.Delete, Payload: (object?)null, Status: 204) })
        {
            var a = await Operation(old, step.Method, "/api/ratings/" + beforeId, step.Payload);
            var b = await Operation(system.Gateway, step.Method, "/api/ratings/" + afterId, step.Payload);
            Assert.Equal(step.Status, a.Status); Assert.Equal(a.Status, b.Status);
            Assert.Equal(Normalize(a.Body), Normalize(b.Body));
            await File.WriteAllTextAsync(System.IO.Path.Combine(directory, step.Name + ".json"), JsonSerializer.Serialize(a, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
        }
    }

    [Fact]
    public async Task Idempotency_ReplaysOneCommittedRatingAndRejectsAnotherPayload()
    {
        var admin = await LoginAsync(ExtractedSystem.AdminEmail);
        var owner = await LoginAsync(ExtractedSystem.OwnerEmail);
        var movie = await SendAsync(HttpMethod.Post, "/api/v1/catalog/items", MoviePayload("Idempotency " + Guid.NewGuid()), 201, admin);
        var id = movie.GetProperty("id").GetGuid(); var key = Guid.NewGuid().ToString("N");
        async Task<(int Status, string Body)> Create(int score)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ratings") { Content = JsonContent.Create(new { movieId = id, score }) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner);
            request.Headers.Add("Idempotency-Key", key);
            using var response = await system.Gateway.SendAsync(request);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        var responses = await Task.WhenAll(Create(7), Create(7));
        Assert.Equal(201, responses[0].Status); Assert.Equal(responses[0], responses[1]);
        var conflict = await Create(8); Assert.Equal(409, conflict.Status);
        Assert.Contains("Idempotency-Key", conflict.Body);
        using var db = new NpgsqlConnection(system.CommunityConnection); await db.OpenAsync();
        using var count = new NpgsqlCommand("SELECT count(*) FROM ratings WHERE movie_id=@id", db); count.Parameters.AddWithValue("id", id);
        Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task DeletedCatalogEpisode_CannotBeRatedThroughAnOldCommunityReference()
    {
        var admin = await LoginAsync(ExtractedSystem.AdminEmail); var owner = await LoginAsync(ExtractedSystem.OwnerEmail);
        var season = await SendAsync(HttpMethod.Post, $"/api/admin/movies/{system.SeriesId}/seasons", new { seasonNumber = 99, title = "Deletion boundary test", description = (string?)null, releaseYear = 2026 }, 201, admin);
        var seasonId = season.GetProperty("id").GetGuid();
        var episode = await SendAsync(HttpMethod.Post, $"/api/admin/seasons/{seasonId}/episodes", new { episodeNumber = 1, title = "Removed episode", description = (string?)null, airDate = (string?)null, duration = 40 }, 201, admin);
        var episodeId = episode.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, "/api/ratings", new { movieId = (Guid?)null, episodeId, score = 8 }, 201, owner);
        await SendAsync(HttpMethod.Delete, $"/api/admin/episodes/{episodeId}", expected: 204, token: admin);
        await SendAsync(HttpMethod.Post, "/api/ratings", new { movieId = (Guid?)null, episodeId, score = 7 }, 404, admin);
    }

    [Fact]
    public async Task GoogleChallenge_UsesGatewayOriginAndVersionedCallback()
    {
        var authUrl = ExtractedSystem.Url(); var gatewayUrl = ExtractedSystem.Url();
        system.Start("CinemaCity.AuthService", authUrl, new()
        {
            ["Jwt__Secret"] = system.JwtSecret, ["Jwt__Issuer"] = "CinemaCity.Tests", ["Jwt__Audience"] = "CinemaCity.Tests",
            ["ConnectionStrings__DefaultConnection"] = system.AuthConnection, ["Database__MigrateOnStartup"] = "false",
            ["Google__ClientId"] = "test-client", ["Google__ClientSecret"] = "test-secret", ["Google__CallbackPath"] = "/api/v1/auth/google/signin"
        });
        await system.WaitReadyAsync(authUrl);
        system.Start("CinemaCity.ApiGateway", gatewayUrl, new() { ["Services__AuthUrl"] = authUrl.ToString() });
        await system.WaitReadyAsync(gatewayUrl);
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = gatewayUrl };
        using var response = await client.GetAsync("/api/auth/google?returnUrl=http%3A%2F%2Flocalhost%3A5173%2Fauth");
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("redirect_uri=" + gatewayUrl + "api/v1/auth/google/signin", Uri.UnescapeDataString(response.Headers.Location!.Query));
    }

    [Fact]
    public async Task StatisticsOutbox_RetriesAfterCatalogRestartAndIgnoresDuplicateVersions()
    {
        var admin = await LoginAsync(ExtractedSystem.AdminEmail);
        var created = await SendAsync(HttpMethod.Post, "/api/v1/catalog/items", MoviePayload("Outbox movie " + Guid.NewGuid().ToString("N")), 201, admin);
        var id = created.GetProperty("id").GetGuid();
        var catalog = system.Apps.Last(app => app.Name == "CinemaCity.CatalogService");
        catalog.Process.Kill(entireProcessTree: true); await catalog.Process.WaitForExitAsync();
        using var db = new NpgsqlConnection(system.CommunityConnection); await db.OpenAsync();
        using var insert = new NpgsqlCommand("INSERT INTO catalog_statistics_outbox (\"Kind\",\"TargetId\",\"AverageRating\",\"RatingsCount\",\"CreatedAt\",\"Attempts\") VALUES ('Movie',@id,7.5,3,CURRENT_TIMESTAMP,0) RETURNING \"Id\"", db);
        insert.Parameters.AddWithValue("id", id);
        var version = (long)(await insert.ExecuteScalarAsync())!;
        var failedDeliveryObserved = false;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            using var check = new NpgsqlCommand("SELECT \"Attempts\" FROM catalog_statistics_outbox WHERE \"Id\"=@version", db); check.Parameters.AddWithValue("version", version);
            if ((int)(await check.ExecuteScalarAsync())! > 0) { failedDeliveryObserved = true; break; }
            await Task.Delay(100);
        }
        var environment = catalog.Process.StartInfo.Environment.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!);
        system.Start("CinemaCity.CatalogService", system.CatalogUrl, environment);
        await system.WaitReadyAsync(system.CatalogUrl);
        Assert.True(failedDeliveryObserved, "The persisted message must survive at least one failed delivery.");
        var delivered = false;
        for (var attempt = 0; attempt < 150; attempt++)
        {
            var movie = await SendAsync(HttpMethod.Get, $"/api/v1/catalog/items/{id}", expected: 200);
            if (movie.GetProperty("ratingsCount").GetInt32() == 3) { Assert.Equal(7.5m, movie.GetProperty("averageRating").GetDecimal()); delivered = true; break; }
            await Task.Delay(100);
        }
        Assert.True(delivered, "The outbox must retry after the owner is available again.");
        using var internalClient = new HttpClient { BaseAddress = system.CatalogUrl };
        internalClient.DefaultRequestHeaders.Add("X-Service-Key", system.InternalKey);
        Assert.Equal(HttpStatusCode.NoContent, (await internalClient.PutAsJsonAsync("/internal/catalog/statistics", new { kind = "Movie", id, averageRating = 0, ratingsCount = 0, version })).StatusCode);
        var unchanged = await SendAsync(HttpMethod.Get, $"/api/v1/catalog/items/{id}", expected: 200);
        Assert.Equal(3, unchanged.GetProperty("ratingsCount").GetInt32());
    }

    [Fact]
    public async Task OfflineImport_AlsoCopiesCommunityDataAndDoesNotResetServiceChanges()
    {
        var destination = system.AdditionalDatabase("copy");
        var environment = new Dictionary<string, string> { ["CINEMACITY_SOURCE_CONNECTION"] = system.SourceConnection, ["CINEMACITY_AUTH_CONNECTION"] = system.AuthConnection, ["CINEMACITY_CATALOG_CONNECTION"] = system.CatalogConnection, ["CINEMACITY_MONOLITH_CONNECTION"] = destination };
        var first = system.Start("CinemaCity.DataMigration", null, environment);
        await first.Process.WaitForExitAsync();
        Assert.True(first.Process.ExitCode == 0, first.Output);
        using var copied = new NpgsqlConnection(destination); await copied.OpenAsync();
        using var check = new NpgsqlCommand("SELECT count(*) FROM reviews WHERE content='Original monolith review'", copied);
        Assert.Equal(1L, await check.ExecuteScalarAsync());
        using var archive = new NpgsqlCommand("SELECT count(*) FROM legacy.movies", copied);
        Assert.True((long)(await archive.ExecuteScalarAsync())! >= 2);
        var second = system.Start("CinemaCity.DataMigration", null, environment);
        await second.Process.WaitForExitAsync();
        Assert.True(second.Process.ExitCode == 0, second.Output);
        Assert.Contains("already imported", second.Output);
    }

    [Fact]
    public async Task Lab0MonolithMode_ForwardsOriginalGetPostAndDeleteWithoutChangingContracts()
    {
        var monoUrl = ExtractedSystem.Url(); var gatewayUrl = ExtractedSystem.Url();
        system.Start("CinemaCity.API", monoUrl, new() { ["ConnectionStrings__DefaultConnection"] = system.SourceConnection, ["Services__Enabled"] = "false", ["Database__MigrateOnStartup"] = "false", ["Jwt__Secret"] = system.JwtSecret, ["Jwt__Issuer"] = "CinemaCity.Tests", ["Jwt__Audience"] = "CinemaCity.Tests", ["Google__ClientId"] = "" });
        await system.WaitReadyAsync(monoUrl);
        system.Start("CinemaCity.ApiGateway", gatewayUrl, new() { ["Gateway__Mode"] = "Monolith", ["Services__MonolithUrl"] = monoUrl.ToString() });
        await system.WaitReadyAsync(gatewayUrl);
        using var gateway = new HttpClient(new HttpClientHandler { UseCookies = false }) { BaseAddress = gatewayUrl };
        using var direct = new HttpClient { BaseAddress = monoUrl };
        var path = $"/api/movies/{system.MovieId}";
        Assert.Equal(await direct.GetStringAsync(path), await gateway.GetStringAsync(path));
        var name = "lab0_" + Guid.NewGuid().ToString("N")[..8];
        using var register = await gateway.PostAsJsonAsync("/api/auth/register", new { username = name, email = name + "@tests.local", password = ExtractedSystem.Password, confirmPassword = ExtractedSystem.Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var session = await register.Content.ReadFromJsonAsync<JsonElement>();
        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/profile/watchlist/{system.MovieId}");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await gateway.SendAsync(delete)).StatusCode);
    }

    [Fact]
    public async Task OriginalDataAndPasswordHashes_ArePreservedAcrossIndependentServices()
    {
        var admin = await LoginAsync(ExtractedSystem.AdminEmail);
        var movie = await SendAsync(HttpMethod.Get, $"/api/movies/{system.MovieId}", expected: 200);
        Assert.Equal("Original CinemaCity film", movie.GetProperty("title").GetString());
        Assert.Equal(system.MovieId, movie.GetProperty("id").GetGuid());
        Assert.Equal("Original CinemaCity actor", movie.GetProperty("cast")[0].GetProperty("fullName").GetString());
        var seasons = await SendAsync(HttpMethod.Get, $"/api/movies/{system.SeriesId}/seasons", expected: 200);
        Assert.Equal(system.EpisodeId, seasons[0].GetProperty("episodes")[0].GetProperty("id").GetGuid());
        var reviews = await SendAsync(HttpMethod.Get, $"/api/users/{system.OwnerId}/reviews", expected: 200);
        Assert.Equal("Original monolith review", reviews[0].GetProperty("content").GetString());
        using var directMonolith = new HttpClient { BaseAddress = system.MonolithUrl };
        Assert.Equal(HttpStatusCode.NotFound, (await directMonolith.GetAsync("/api/movies")).StatusCode);
        using var authDb = new NpgsqlConnection(system.AuthConnection); await authDb.OpenAsync();
        using var hash = new NpgsqlCommand("SELECT password_hash FROM users WHERE email=@email", authDb); hash.Parameters.AddWithValue("email", ExtractedSystem.AdminEmail);
        var stored = (string)(await hash.ExecuteScalarAsync())!;
        Assert.NotEqual(ExtractedSystem.Password, stored);
        using var authClient = new HttpClient { BaseAddress = system.AuthUrl };
        var schema = await authClient.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.True(schema.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        Assert.True(schema.GetProperty("paths").GetProperty("/api/v1/auth/register").GetProperty("post").TryGetProperty("summary", out _));
        Assert.DoesNotContain("PasswordHash", schema.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticationValidation_Roles_Rotation_Logout_AndExpiredJwt()
    {
        var name = "auth_" + Guid.NewGuid().ToString("N")[..10]; var email = name + "@tests.local";
        var payload = new { username = name, email, password = ExtractedSystem.Password, confirmPassword = ExtractedSystem.Password };
        var session = await SendAsync(HttpMethod.Post, "/api/v1/auth/register", payload, 201);
        var token = session.GetProperty("accessToken").GetString()!; var refresh = session.GetProperty("refreshToken").GetString()!;
        await SendAsync(HttpMethod.Post, "/api/v1/auth/register", payload, 409);
        var invalid = await SendAsync(HttpMethod.Post, "/api/v1/auth/register", new { username = "short", email = "short@tests.local", password = "x", confirmPassword = "x" }, 400);
        Assert.True(invalid.TryGetProperty("errors", out _));
        await SendAsync(HttpMethod.Post, "/api/v1/auth/register", new { username = (string?)null, email, password = ExtractedSystem.Password, confirmPassword = ExtractedSystem.Password }, 400);
        await SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password = "incorrect-password" }, 401);
        await SendAsync(HttpMethod.Get, "/api/v1/users/me", expected: 401);
        var me = await SendAsync(HttpMethod.Get, "/api/v1/users/me", expected: 200, token: token);
        Assert.Equal(email, me.GetProperty("email").GetString());
        await SendAsync(HttpMethod.Get, "/api/v1/users", expected: 403, token: token);
        var admin = await LoginAsync(ExtractedSystem.AdminEmail);
        await SendAsync(HttpMethod.Get, "/api/v1/users?page=1&pageSize=2", expected: 200, token: admin);
        await SendAsync(HttpMethod.Put, $"/api/v1/users/{me.GetProperty("id").GetGuid()}/roles", new { roles = new[] { "User", "Admin" } }, 204, admin);
        var renewed = await SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", new { refreshToken = refresh }, 200);
        Assert.NotEqual(refresh, renewed.GetProperty("refreshToken").GetString());
        await SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", new { refreshToken = refresh }, 401);
        await SendAsync(HttpMethod.Post, "/api/v1/auth/logout", new { refreshToken = renewed.GetProperty("refreshToken").GetString() }, 401);
        await SendAsync(HttpMethod.Post, "/api/v1/auth/logout", new { refreshToken = renewed.GetProperty("refreshToken").GetString() }, 204, renewed.GetProperty("accessToken").GetString());
        await SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", new { refreshToken = renewed.GetProperty("refreshToken").GetString() }, 401);
        var jwt = new JwtSecurityToken("CinemaCity.Tests", "CinemaCity.Tests", [new Claim(ClaimTypes.Role, "Admin")], DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(system.JwtSecret)), SecurityAlgorithms.HmacSha256));
        await SendAsync(HttpMethod.Post, "/api/v1/catalog/items", MoviePayload("Expired token"), 401, new JwtSecurityTokenHandler().WriteToken(jwt));
    }

    [Fact]
    public async Task Catalog_ValidatesPagesAndRelations_AndEnforcesAdminCrud()
    {
        var owner = await LoginAsync(ExtractedSystem.OwnerEmail); var admin = await LoginAsync(ExtractedSystem.AdminEmail);
        var title = "Created movie " + Guid.NewGuid().ToString("N"); var payload = MoviePayload(title);
        await SendAsync(HttpMethod.Post, "/api/v1/catalog/items", payload, 401);
        await SendAsync(HttpMethod.Post, "/api/v1/catalog/items", payload, 403, owner);
        await SendAsync(HttpMethod.Get, "/api/v1/catalog/items?page=0", expected: 400);
        await SendAsync(HttpMethod.Get, "/api/v1/catalog/items?pageSize=10000", expected: 400);
        await SendAsync(HttpMethod.Get, "/api/v1/catalog/items?sortBy=invalid", expected: 400);
        await SendAsync(HttpMethod.Post, "/api/v1/catalog/items", MoviePayload("Invalid genre", [int.MaxValue]), 400, admin);
        var oversizedLegacyTitle = JsonSerializer.SerializeToNode(MoviePayload("Legacy validation"))!;
        oversizedLegacyTitle["originalTitle"] = new string('x', 256);
        await SendAsync(HttpMethod.Post, "/api/admin/movies", oversizedLegacyTitle, 400, admin);
        var created = await SendAsync(HttpMethod.Post, "/api/v1/catalog/items", payload, 201, admin);
        var id = created.GetProperty("id").GetGuid();
        var page = await SendAsync(HttpMethod.Get, $"/api/v1/catalog/items?page=1&pageSize=1&search={Uri.EscapeDataString(title)}&categoryId=1&sortBy=title&desc=false", expected: 200);
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32()); Assert.Equal(id, page.GetProperty("items")[0].GetProperty("id").GetGuid());
        await SendAsync(HttpMethod.Put, $"/api/v1/catalog/items/{id}", MoviePayload(title + " updated"), 204, admin);
        await SendAsync(HttpMethod.Delete, $"/api/v1/catalog/items/{id}", expected: 204, token: admin);
        await SendAsync(HttpMethod.Get, $"/api/v1/catalog/items/{id}", expected: 404);
        Assert.Contains("LIMIT", system.Apps.Last(a => a.Name == "CinemaCity.CatalogService").Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewServiceAccountsAndMovies_WorkInOriginalCommunityFeatures()
    {
        var name = "community_" + Guid.NewGuid().ToString("N")[..8];
        var session = await SendAsync(HttpMethod.Post, "/api/auth/register", new { username = name, email = name + "@tests.local", password = ExtractedSystem.Password, confirmPassword = ExtractedSystem.Password }, 201);
        var token = session.GetProperty("accessToken").GetString()!;
        var admin = await LoginAsync(ExtractedSystem.AdminEmail);
        var created = await SendAsync(HttpMethod.Post, "/api/admin/movies", MoviePayload("Community movie " + Guid.NewGuid().ToString("N")), 201, admin);
        var id = created.GetProperty("id").GetGuid();
        await SendAsync(HttpMethod.Post, "/api/v1/ratings", new { movieId = id, episodeId = (Guid?)null, score = 9 }, 201, token);
        var watched = await SendAsync(HttpMethod.Get, "/api/profile/watchlist?status=Watched", expected: 200, token: token);
        Assert.Contains(watched.EnumerateArray(), entry => entry.GetProperty("movieId").GetGuid() == id);
        await SendAsync(HttpMethod.Post, "/api/ratings", new { movieId = (Guid?)null, episodeId = system.EpisodeId, score = 8 }, 201, token);
        var seasons = await SendAsync(HttpMethod.Get, $"/api/movies/{system.SeriesId}/seasons", expected: 200, token: token);
        Assert.Equal(8, seasons[0].GetProperty("episodes")[0].GetProperty("userRating").GetProperty("score").GetDecimal());
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var movie = await SendAsync(HttpMethod.Get, $"/api/v1/catalog/items/{id}", expected: 200);
            if (movie.GetProperty("ratingsCount").GetInt32() == 1) { Assert.Equal(9, movie.GetProperty("averageRating").GetDecimal()); return; }
            await Task.Delay(100);
        }
        Assert.Fail("The rating outbox did not update catalog statistics.");
    }

    [Fact]
    public async Task GatewayPriority_Correlation_Cors_AndDownstreamFailure_AreObservable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/catalog/categories");
        request.Headers.Add("X-Correlation-Id", "cinemacity-extraction-test"); request.Headers.Add("Origin", "http://localhost:5173");
        using var response = await system.Gateway.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("cinemacity-extraction-test", response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Contains("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin"));
        var badGateway = ExtractedSystem.Url();
        system.Start("CinemaCity.ApiGateway", badGateway, new() { ["Services__CatalogUrl"] = ExtractedSystem.Url().ToString(), ["Services__MonolithUrl"] = system.MonolithUrl.ToString(), ["Services__CommunityUrl"] = system.MonolithUrl.ToString(), ["Services__AuthUrl"] = system.AuthUrl.ToString() });
        await system.WaitReadyAsync(badGateway);
        using var client = new HttpClient { BaseAddress = badGateway };
        using var failed = await client.GetAsync("/api/movies");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var body = await failed.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(503, body.GetProperty("status").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/home/popular-now")).StatusCode);
        Assert.Contains("cinemacity-extraction-test", system.Apps.First(a => a.Name == "CinemaCity.ApiGateway").Output);
    }

    [Fact]
    public async Task Health_Reports503WhenItsOwnDatabaseIsUnavailable()
    {
        var url = ExtractedSystem.Url();
        var connection = new NpgsqlConnectionStringBuilder(system.AuthConnection) { Port = ExtractedSystem.Url().Port, Timeout = 1 };
        system.Start("CinemaCity.AuthService", url, new() { ["Jwt__Secret"] = system.JwtSecret, ["Jwt__Issuer"] = "CinemaCity.Tests", ["Jwt__Audience"] = "CinemaCity.Tests", ["ConnectionStrings__DefaultConnection"] = connection.ConnectionString, ["Database__MigrateOnStartup"] = "false", ["Google__ClientId"] = "" });
        await system.WaitReadyAsync(url, expected: 503);
    }

    private async Task<string> LoginAsync(string email)
    {
        var result = await SendAsync(HttpMethod.Post, "/api/v1/auth/login", new { email, password = ExtractedSystem.Password }, 200);
        return result.GetProperty("accessToken").GetString()!;
    }
    private static object MoviePayload(string title, int[]? genres = null) => new { title, originalTitle = title, contentType = "Movie", releaseYear = 2026, duration = 100, description = "CinemaCity extracted movie", posterUrl = (string?)null, backdropUrl = (string?)null, trailerYoutubeId = (string?)null, genreIds = genres ?? [1], countryIds = Array.Empty<int>(), languageIds = Array.Empty<int>(), cast = Array.Empty<object>() };
    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body = null, int expected = 200, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await system.Gateway.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == expected, $"{method} {path}: expected {expected}, got {(int)response.StatusCode}: {text}\n" + (response.IsSuccessStatusCode ? "" : string.Join("\n", system.Apps.Select(app => app.Name + ":\n" + app.Output[^Math.Min(6000, app.Output.Length)..]))));
        return string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<JsonElement>(text);
    }
}
