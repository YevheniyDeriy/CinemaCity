using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CinemaCity.ServiceDefaults;

/// <summary>Lets thin MVC controllers execute the extracted monolith workflows without changing success DTOs.</summary>
public sealed class HttpResultAction(IResult result) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context)
    {
        if (result is IStatusCodeHttpResult { StatusCode: >= 400 } status && result is IValueHttpResult { Value: not null } value
            && value.Value is not ProblemDetails && value.Value is not HttpValidationProblemDetails)
        {
            var message = value.Value.GetType().GetProperty("Message")?.GetValue(value.Value)?.ToString();
            if (status.StatusCode == 400)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message ?? "Invalid request."] }).ExecuteAsync(context.HttpContext);
            return Results.Problem(message, statusCode: status.StatusCode).ExecuteAsync(context.HttpContext);
        }
        return result.ExecuteAsync(context.HttpContext);
    }
}
