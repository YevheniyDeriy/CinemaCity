using System.Diagnostics;

namespace CinemaCity.ApiGateway;

internal sealed class GatewayMiddleware(RequestDelegate next, ILogger<GatewayMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-Id"].ToString();
        var correlation = supplied.Length is > 0 and <= 100 && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') ? supplied : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlation;
        context.Request.Headers["X-Correlation-Id"] = correlation;
        context.Response.Headers["X-Correlation-Id"] = correlation;
        var started = Stopwatch.GetTimestamp();
        try
        {
            // Short-circuit before the terminal Ocelot pipeline; this is process liveness, not downstream readiness.
            if (context.Request.Path == "/health")
            {
                await context.Response.WriteAsJsonAsync(new { service = "CinemaCity.ApiGateway", status = "Healthy" });
                return;
            }
            await next(context);
            if (!context.Response.HasStarted && context.Response.StatusCode is 502 or 503 or 504)
            {
                await GatewayPolicies.Problem(context, 503, "The requested service is unavailable", context.RequestAborted);
            }
        }
        finally
        {
            logger.LogInformation("{Method} {Path} -> {Destination} {Status} {Elapsed:F1}ms [{CorrelationId}]", context.Request.Method, context.Request.Path,
                context.Items.TryGetValue("Downstream", out var downstream) ? downstream : "gateway", context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds, correlation);
        }
    }
}

internal sealed class DownstreamLoggingHandler(IHttpContextAccessor accessor, IConfiguration configuration, CatalogCache cache) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext;
        if (context is not null)
        {
            // No query string, credentials, request bodies, passwords or tokens are logged.
            context.Items["Downstream"] = request.RequestUri?.GetLeftPart(UriPartial.Path) ?? "unknown";
            request.Headers.Remove("X-Correlation-Id");
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", context.TraceIdentifier);
            // Preserve the public origin for the original Google OAuth callback and absolute URL generation.
            request.Headers.Host = context.Request.Host.Value;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(configuration.GetValue("Gateway:TimeoutSeconds", 8)));
        try
        {
            var response = await base.SendAsync(request, deadline.Token);
            try { await response.Content.LoadIntoBufferAsync(deadline.Token); }
            catch { response.Dispose(); throw; }
            if (response.IsSuccessStatusCode && request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && context is not null &&
                (context.Request.Path.StartsWithSegments("/api/admin") || context.Request.Path.StartsWithSegments("/api/v1/admin") || context.Request.Path.StartsWithSegments("/api/v1/catalog")))
                cache.Invalidate();
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new HttpRequestException("Downstream deadline exceeded."); }
    }
}
