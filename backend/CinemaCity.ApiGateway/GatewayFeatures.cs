using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CinemaCity.ApiGateway;

public static class GatewayPolicies
{
    public static void ConfigureLimits(RateLimiterOptions options, IConfiguration config)
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var login = context.Request.Path.Value?.TrimEnd('/').EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase) == true;
            var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var group = login ? "login" : user is null ? "anonymous" : "user";
            var identity = login || user is null ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown" : user;
            return RateLimitPartition.GetFixedWindowLimiter(group + ":" + identity, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = config.GetValue($"Gateway:Limits:{group}", login ? 5 : user is null ? 120 : 240),
                Window = TimeSpan.FromSeconds(config.GetValue("Gateway:Limits:WindowSeconds", 60)),
                QueueLimit = 0, AutoReplenishment = true
            });
        });
        options.RejectionStatusCode = 429;
        options.OnRejected = async (context, ct) =>
        {
            var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : config.GetValue("Gateway:Limits:WindowSeconds", 60);
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            await Problem(context.HttpContext, 429, "Request limit exceeded", ct);
        };
    }
    public static Task Problem(HttpContext context, int status, string title, CancellationToken ct = default)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { type = "about:blank", title, status, traceId = context.TraceIdentifier }, options: null, contentType: "application/problem+json", cancellationToken: ct);
    }
    public static bool RequiresUser(HttpContext context)
    {
        var path = context.Request.Path.Value!.ToLowerInvariant().Replace("/api/v1/", "/api/").TrimEnd('/');
        return path.StartsWith("/api/screens/") || path.StartsWith("/api/profile") || path.StartsWith("/api/admin") ||
            path.StartsWith("/api/ratings") || path is "/api/users/me" or "/api/auth/me" or "/api/auth/logout" or "/api/users" ||
            path.EndsWith("/roles") || (path.StartsWith("/api/reviews") && !HttpMethods.IsGet(context.Request.Method)) ||
            (path.StartsWith("/api/catalog") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method));
    }
}

public sealed class GatewayFeaturesMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IEnumerable<IScreenComposer> screens, CatalogCache cache, ScreenClients clients, IConfiguration config, IHttpClientFactory factory)
    {
        var ct = context.RequestAborted;
        try
        {
            if (context.Request.Path == "/health/ready")
            {
                var checks = await Task.WhenAll(new[] { "Auth", "Catalog", "Community" }.Select(async service =>
                {
                    try { using var response = await factory.CreateClient(service).GetAsync("/health", ct); return new { service, available = response.IsSuccessStatusCode }; }
                    catch (HttpRequestException) { return new { service, available = false }; }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new { service, available = false }; }
                }));
                context.Response.StatusCode = checks.All(c => c.available) ? 200 : 503;
                await context.Response.WriteAsJsonAsync(new { services = checks }, ct); return;
            }
            if (context.Request.Path.StartsWithSegments("/api") &&
                ((context.Request.Headers.ContainsKey("Authorization") && context.User.Identity?.IsAuthenticated != true) ||
                 (GatewayPolicies.RequiresUser(context) && context.User.Identity?.IsAuthenticated != true)))
            { await GatewayPolicies.Problem(context, 401, "A valid access token is required", ct); return; }
            // Never trust caller-supplied identity headers. Services receive the original validated JWT.
            context.Request.Headers.Remove("X-User-Id"); context.Request.Headers.Remove("X-User-Roles");
            foreach (var screen in screens)
                if (HttpMethods.IsGet(context.Request.Method) && screen.Matches(context.Request.Path, out var id))
                { await (await screen.ComposeAsync(id, context, ct)).ExecuteAsync(context); return; }
            await next(context);

        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !context.Response.HasStarted) { await GatewayPolicies.Problem(context, 504, "Downstream request timed out", ct); }
        catch (HttpRequestException) when (!context.Response.HasStarted) { await GatewayPolicies.Problem(context, 503, "A downstream service is unavailable", ct); }
    }
}
