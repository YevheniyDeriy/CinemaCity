using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CinemaCity.API.Integration;

public sealed class OperationReplayDocumentation : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.HttpMethod != "POST") return;
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key", In = ParameterLocation.Header, Required = false,
            Description = "Optional key, up to 128 characters. Repeating the same authenticated request returns its committed status, JSON and Location. A changed payload returns 409. Requests without a key preserve the original contract.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 128 }
        });
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("409", new OpenApiResponse { Description = "The key already identifies a different request, or the operation conflicts with existing data." });
        operation.Responses.TryAdd("503", new OpenApiResponse { Description = "The catalog or another dependency is unavailable or timed out." });
    }
}
