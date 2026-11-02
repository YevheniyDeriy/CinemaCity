using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CinemaCity.Gateway.Tests;

public sealed class GatewayTests : IAsyncLifetime
{
    private WebApplication stub = null!;
    private GatewayFactory factory = null!;
    private HttpClient client = null!;
    private readonly Guid movie = Guid.NewGuid();
    private int catalogCalls, personalCalls, active, maxActive, loginCalls;
    private bool unavailable, slow;
    private string title = "CinemaCity film";
    private const string Secret = "gateway-integration-test-signing-key-at-least-32-bytes";

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        stub = builder.Build();
        stub.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
        stub.MapGet("/api/v1/catalog/items/{id:guid}", async (Guid id, HttpContext context) =>
        {
            Interlocked.Increment(ref catalogCalls); await Delay(context.RequestAborted);
            return Results.Ok(new { id, title, releaseYear = 2026, posterUrl = (string?)null });
        });
        stub.MapPut("/api/v1/catalog/items/{id:guid}", () => { title = "Updated film"; return Results.NoContent(); });
        stub.MapGet("/api/profile/movies/{id:guid}/state", async (HttpContext context) =>
        {
            Interlocked.Increment(ref personalCalls); await Delay(context.RequestAborted);
            if (unavailable) return Results.StatusCode(503);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(context.Request.Headers.Authorization.ToString()[7..]);
            return Results.Ok(new { owner = jwt.Subject, rating = new { score = jwt.Subject == "alice" ? 8 : 4 }, lists = Array.Empty<object>() });
        });
        stub.MapPost("/api/v1/auth/login", () => { Interlocked.Increment(ref loginCalls); return Results.Ok(new { }); });
        await stub.StartAsync();
        var url = stub.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        factory = new GatewayFactory(url);
        var settings = new Dictionary<string, string>
        {
            ["Jwt__Secret"] = Secret, ["Jwt__Issuer"] = "Gateway.Tests", ["Jwt__Audience"] = "Gateway.Tests",
            ["Services__AuthUrl"] = url, ["Services__CatalogUrl"] = url, ["Services__CommunityUrl"] = url,
            ["Gateway__Mode"] = "Services", ["Gateway__TimeoutSeconds"] = "1", ["Gateway__Limits__login"] = "2"
        };
        var previous = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try { foreach (var pair in settings) Environment.SetEnvironmentVariable(pair.Key, pair.Value); client = factory.CreateClient(); }
        finally { foreach (var pair in previous) Environment.SetEnvironmentVariable(pair.Key, pair.Value); }
    }
    private async Task Delay(CancellationToken ct)
    {
        var value = Interlocked.Increment(ref active); InterlockedExtensions.Max(ref maxActive, value);
        try { await Task.Delay(slow ? 2500 : 100, ct); } finally { Interlocked.Decrement(ref active); }
    }
    private static string Token(string user) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("Gateway.Tests", "Gateway.Tests", [new Claim("sub", user), new Claim(ClaimTypes.Role, "Admin")], expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));
    private async Task<HttpResponseMessage> Screen(string? user = "alice")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/screens/movies/{movie}");
        if (user is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));
        return await client.SendAsync(request);
    }
    [Fact]
    public async Task Composition_IsParallelAndComplete()
    {
        using var response = await Screen(); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(movie, json.GetProperty("movie").GetProperty("id").GetGuid());
        Assert.Equal(8, json.GetProperty("myState").GetProperty("myScore").GetInt32());
        Assert.False(json.GetProperty("partial").GetBoolean()); Assert.True(maxActive >= 2);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }
    [Fact]
    public async Task MissingCommunity_IsExplicitlyPartial()
    {
        unavailable = true; using var response = await Screen();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.GetProperty("partial").GetBoolean()); Assert.Equal("unavailable", json.GetProperty("communityStatus").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("myState").ValueKind);
    }
    [Fact]
    public async Task Anonymous_IsRejectedBeforeEitherDownstream()
    {
        using var response = await Screen(null); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(401, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
        Assert.Equal(0, catalogCalls); Assert.Equal(0, personalCalls);
    }
    [Fact]
    public async Task Cache_IsSharedButPersonalStateIsFetchedForEachUser()
    {
        using var first = await Screen("alice"); using var second = await Screen("bob");
        Assert.Equal(1, catalogCalls); Assert.Equal(2, personalCalls);
        var a = await first.Content.ReadFromJsonAsync<JsonElement>(); var b = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(8, a.GetProperty("myState").GetProperty("myScore").GetInt32());
        Assert.Equal(4, b.GetProperty("myState").GetProperty("myScore").GetInt32());
    }
    [Fact]
    public async Task SuccessfulMutation_InvalidatesPublicCatalogCache()
    {
        using var first = await Screen();
        using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/catalog/items/{movie}") { Content = JsonContent.Create(new { title = "Updated film" }) };
        put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token("alice"));
        using var changed = await client.SendAsync(put); Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var second = await Screen(); Assert.Equal(2, catalogCalls);
        Assert.Equal("Updated film", (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("movie").GetProperty("title").GetString());
    }
    [Fact]
    public async Task LoginHasStricterLimit_WithRetryAfterAndProblemDetails()
    {
        for (var i = 0; i < 2; i++) { using var ok = await client.PostAsJsonAsync("/api/auth/login", new { }); Assert.Equal(HttpStatusCode.OK, ok.StatusCode); }
        using var rejected = await client.PostAsJsonAsync("/api/auth/login", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode); Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal(429, (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32()); Assert.Equal(2, loginCalls);
    }
    [Fact]
    public async Task SlowCatalog_HasDeadlineAndReturnsProblemDetails()
    {
        slow = true; using var response = await Screen(); Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal(504, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); await stub.DisposeAsync(); }
    private sealed class GatewayFactory(string url) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:AuthUrl"] = url, ["Services:CatalogUrl"] = url, ["Services:CommunityUrl"] = url,
                ["Gateway:Mode"] = "Services", ["Jwt:Secret"] = Secret, ["Jwt:Issuer"] = "Gateway.Tests", ["Jwt:Audience"] = "Gateway.Tests",
                ["Gateway:TimeoutSeconds"] = "1", ["Gateway:Limits:login"] = "2"
            }));
        }
    }
}
internal static class InterlockedExtensions
{
    public static void Max(ref int target, int value) { int current; do { current = Volatile.Read(ref target); if (current >= value) return; } while (Interlocked.CompareExchange(ref target, value, current) != current); }
}
