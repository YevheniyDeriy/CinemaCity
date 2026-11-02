using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CinemaCity.ServiceDefaults;

/// <summary>Technical defaults only: this assembly contains no domain entities or business logic.</summary>
public static class ServiceDefaults
{
    public static IServiceCollection AddServiceDefaults(this IServiceCollection services, IConfiguration configuration, string title)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1", Description = "Extracted from the CinemaCity monolith." });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
            options.OperationFilter<EndpointDocumentationFilter>();
            options.SchemaFilter<DtoDocumentationFilter>();
            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "CinemaCity.*.xml"))
                options.IncludeXmlComments(file, includeControllerXmlComments: true);
        });
        services.AddHttpContextAccessor();
        services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        return services;
    }

    /// <summary>Locally verifies a token; never calls the issuer or a remote database.</summary>
    public static IServiceCollection AddLocalJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Set Jwt:Secret to at least 32 bytes using user-secrets or environment variables.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"], ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role, ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
        services.AddAuthorization(options => options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin")));
        return services;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationMiddleware>();
        app.UseCors();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    public static RouteHandlerBuilder MapDatabaseHealth<T>(this IEndpointRouteBuilder routes, string service) where T : DbContext =>
        routes.MapGet("/health", async (IServiceProvider provider, CancellationToken ct) =>
        {
            var db = provider.GetRequiredService<T>();
            var available = await db.Database.CanConnectAsync(ct);
            return Results.Json(new DatabaseHealthDto(service, available ? "Healthy" : "Unavailable", available), statusCode: available ? 200 : 503);
        }).WithSummary("Application and database readiness").Produces<DatabaseHealthDto>(200).Produces<DatabaseHealthDto>(503).AllowAnonymous();

    public static bool IsInternalRequest(HttpContext context, IConfiguration configuration)
    {
        var expected = configuration["Services:InternalKey"];
        var actual = context.Request.Headers["X-Service-Key"].ToString();
        return !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(actual)
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }
}

/// <summary>Application and its own database readiness; no cross-service database access.</summary>
public sealed record DatabaseHealthDto(
    [property: System.ComponentModel.Description("Application name.")] string Service,
    [property: System.ComponentModel.Description("Healthy or Unavailable.")] string Status,
    [property: System.ComponentModel.Description("Whether the application's own database is reachable.")] bool Database);

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is ValidationException validation)
        {
            var field = validation.Data["field"] as string ?? "request";
            await Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [validation.Message] }).ExecuteAsync(context);
            return true;
        }
        var status = exception switch
        {
            ValidationException or ArgumentException or JsonException => 400,
            KeyNotFoundException => 404,
            DbUpdateConcurrencyException => 409,
            DbUpdateException when exception.InnerException?.GetType().GetProperty("SqlState")?.GetValue(exception.InnerException)?.ToString() == "23505" => 409,
            HttpRequestException or TimeoutException => 503,
            BadHttpRequestException bad => bad.StatusCode,
            _ => 500
        };
        if (status >= 500) logger.LogError(exception, "Request failed: {Method} {Path}", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = status switch { 400 => "Invalid request", 404 => "Resource not found", 409 => "Conflict", 503 => "A dependency is unavailable", _ => "An unexpected error occurred" }, Extensions = { ["traceId"] = context.TraceIdentifier } }
        });
    }
}

public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-Id"].ToString();
        var correlationId = supplied.Length is > 0 and <= 100 && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') ? supplied : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Request.Headers["X-Correlation-Id"] = correlationId;
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        var started = Stopwatch.GetTimestamp();
        try { await next(context); }
        finally { logger.LogInformation("{Method} {Path} -> {Status} in {Elapsed:F1} ms [{CorrelationId}]", context.Request.Method, context.Request.Path, context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds, correlationId); }
    }
}

/// <summary>Runs DataAnnotations on public request DTOs, including nested collections.</summary>
public sealed class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var argument in context.Arguments.Where(a => a is not null && a.GetType().Name.EndsWith("Request"))) Validate(argument!, "", errors);
        return errors.Count > 0 ? Results.ValidationProblem(errors) : await next(context);
    }

    private static void Validate(object value, string prefix, Dictionary<string, string[]> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        foreach (var result in results)
            foreach (var member in result.MemberNames.DefaultIfEmpty("request")) errors[prefix + member] = [result.ErrorMessage ?? "Invalid value"];
        foreach (var property in value.GetType().GetProperties())
        {
            if (property.GetValue(value) is System.Collections.IEnumerable collection && property.PropertyType != typeof(string))
            {
                var index = 0;
                foreach (var item in collection)
                {
                    if (item is not null && item.GetType().Name.EndsWith("Request")) Validate(item, $"{prefix}{property.Name}[{index}].", errors);
                    index++;
                }
            }
        }
    }
}

/// <summary>Normalizes errors from the original workflows without changing successful response contracts.</summary>
public sealed class ProblemResultFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 } status && result is IValueHttpResult { Value: not null } value
            && value.Value is not ProblemDetails && value.Value is not HttpValidationProblemDetails)
        {
            var message = value.Value.GetType().GetProperty("Message")?.GetValue(value.Value)?.ToString();
            if (status.StatusCode == 400) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message ?? "Invalid request."] });
            return Results.Problem(message, statusCode: status.StatusCode, extensions: new Dictionary<string, object?> { ["traceId"] = context.HttpContext.TraceIdentifier });
        }
        return result;
    }
}

internal sealed class EndpointDocumentationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        operation.Summary ??= metadata.OfType<IEndpointSummaryMetadata>().FirstOrDefault()?.Summary ?? context.ApiDescription.ActionDescriptor.DisplayName?.Split('.').Last();
        if (metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any() && !metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
        {
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "A valid access token is required." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "The current user has insufficient permissions." });
            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [] }];
        }
    }
}

internal sealed class DtoDocumentationFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete) return;
        concrete.Description ??= context.Type.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
        if (concrete.Properties is null) return;
        foreach (var property in context.Type.GetProperties())
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (concrete.Properties.TryGetValue(name, out var item) && item is OpenApiSchema field)
                field.Description ??= property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
        }
    }
}
