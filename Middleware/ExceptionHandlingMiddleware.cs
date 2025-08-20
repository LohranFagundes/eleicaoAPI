using ElectionApi.Net.DTOs;
using System.Net;
using System.Text.Json;

namespace ElectionApi.Net.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ApiResponse<object>();

        switch (exception)
        {
            case UnauthorizedAccessException:
                response = ApiResponse<object>.ErrorResult("Unauthorized access");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                break;
            case ArgumentException:
                response = ApiResponse<object>.ErrorResult("Invalid argument provided");
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                break;
            case KeyNotFoundException:
                response = ApiResponse<object>.ErrorResult("Resource not found");
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                break;
            default:
                response = ApiResponse<object>.ErrorResult("An internal server error occurred");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}