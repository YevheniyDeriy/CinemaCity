using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace CinemaCity.ServiceDefaults;

/// <summary>Normalizes legacy API error envelopes while retaining their message extension.</summary>
public sealed class ProblemResponseMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
            if (context.Response.StatusCode >= 400 && !context.Response.HasStarted)
            {
                buffer.Position = 0;
                JsonObject problem;
                try { problem = JsonNode.Parse(buffer)?.AsObject() ?? new JsonObject(); }
                catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException) { problem = new JsonObject(); }
                var status = context.Response.StatusCode == 502 ? 503 : context.Response.StatusCode;
                context.Response.StatusCode = status;
                var message = problem["message"]?.ToString() ?? problem["detail"]?.ToString();
                if (status >= 500) { problem = new JsonObject(); message = "A required service is unavailable. Please retry later."; }
                problem["type"] ??= "about:blank";
                problem["title"] ??= ReasonPhrases.GetReasonPhrase(status);
                problem["status"] = status;
                if (message is not null) problem["detail"] = message;
                if (status == 400 && problem["errors"] is null)
                {
                    var field = message?.Contains("Score", StringComparison.OrdinalIgnoreCase) == true ? "score" : "request";
                    problem["errors"] = new JsonObject { [field] = new JsonArray(message ?? "Invalid request.") };
                }
                buffer.SetLength(0);
                await System.Text.Json.JsonSerializer.SerializeAsync(buffer, problem, cancellationToken: context.RequestAborted);
                context.Response.ContentType = "application/problem+json";
                context.Response.ContentLength = buffer.Length;
            }
            buffer.Position = 0;
            await buffer.CopyToAsync(original, context.RequestAborted);
        }
        finally { context.Response.Body = original; }
    }
}
