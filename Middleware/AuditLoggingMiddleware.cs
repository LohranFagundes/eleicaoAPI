
using System.Security.Claims;
using System.Text;
using ElectionApi.Net.Services;

namespace ElectionApi.Net.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        // Skip logging for certain paths
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var startTime = DateTime.UtcNow;
        var originalResponseBody = context.Response.Body;

        try
        {
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            // Log the request
            await LogRequestAsync(context, auditService, duration, context.Response.StatusCode);

            // Copy response back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalResponseBody);
        }
        catch (Exception ex)
        {
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            // Log the error
            await LogErrorAsync(context, auditService, ex, duration);

            throw; // Re-throw the exception
        }
        finally
        {
            context.Response.Body = originalResponseBody;
        }
    }

    private bool ShouldSkipLogging(PathString path)
    {
        var pathsToSkip = new[]
        {
            "/health",
            "/swagger",
            "/api/report/real-time", // Avoid logging the real-time logs endpoint itself
            "/_framework",
            "/css",
            "/js",
            "/images",
            "/uploads"
        };

        return pathsToSkip.Any(skipPath => path.StartsWithSegments(skipPath));
    }

    private async Task LogRequestAsync(HttpContext context, IAuditService auditService, TimeSpan duration, int statusCode)
    {
        try
        {
            var userId = GetCurrentUserId(context);
            var userType = GetCurrentUserType(context);
            var action = DetermineAction(context, statusCode);
            var entityInfo = ExtractEntityInfo(context);

            var details = $"{{" +
                         $"\"method\": \"{context.Request.Method}\", " +
                         $"\"path\": \"{context.Request.Path}\", " +
                         $"\"queryString\": \"{context.Request.QueryString}\", " +
                         $"\"statusCode\": {statusCode}, " +
                         $"\"duration\": \"{duration.TotalMilliseconds}ms\", " +
                         $"\"userAgent\": \"{context.Request.Headers["User-Agent"].FirstOrDefault()}\"" +
                         $"}}";

            if (ShouldLogRequest(context.Request.Method, statusCode))
            {
                await auditService.LogAsync(userId, userType, action, entityInfo.EntityType, entityInfo.EntityId, details);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log request audit information");
        }
    }

    private async Task LogErrorAsync(HttpContext context, IAuditService auditService, Exception ex, TimeSpan duration)
    {
        try
        {
            var userId = GetCurrentUserId(context);
            var userType = GetCurrentUserType(context);

            var details = $"{{" +
                         $"\"method\": \"{context.Request.Method}\", " +
                         $"\"path\": \"{context.Request.Path}\", " +
                         $"\"error\": \"{ex.Message}\", " +
                         $"\"exception\": \"{ex.GetType().Name}\", " +
                         $"\"duration\": \"{duration.TotalMilliseconds}ms\"" +
                         $"}}";

            await auditService.LogAsync(userId, userType, "error", "api_request", null, details);
        }
        catch (Exception logEx)
        {
            _logger.LogError(logEx, "Failed to log error audit information");
        }
    }

    private int? GetCurrentUserId(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private string GetCurrentUserType(HttpContext context)
    {
        if (!context.User.Identity?.IsAuthenticated == true)
            return "anonymous";

        var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
        return roleClaim switch
        {
            "admin" => "admin",
            "voter" => "voter",
            _ => "authenticated"
        };
    }

        private string DetermineAction(HttpContext context, int statusCode)
    {
        var method = context.Request.Method.ToUpper();
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var isSuccess = statusCode >= 200 && statusCode < 400;

        if (path.Contains("login"))
        {
            return isSuccess ? "login_success" : "login_failed";
        }

        if (path.Contains("logout"))
        {
            return isSuccess ? "logout_success" : "logout_failed";
        }

        return method switch
        {
            "GET" => isSuccess ? "api_read" : "api_read_failed",
            "POST" => isSuccess ? "api_create" : "api_create_failed",
            "PUT" => isSuccess ? "api_update" : "api_update_failed",
            "PATCH" => isSuccess ? "api_patch" : "api_patch_failed",
            "DELETE" => isSuccess ? "api_delete" : "api_delete_failed",
            _ => isSuccess ? "api_request" : "api_request_failed"
        };
    }

    private (string EntityType, int? EntityId) ExtractEntityInfo(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2 || segments[0] != "api")
            return ("api_request", null);

        var controller = segments[1];
        var entityType = controller switch
        {
            "auth" => "authentication",
            "election" => "elections",
            "position" => "positions",
            "candidate" => "candidates",
            "voter" => "voters",
            "report" => "reports",
            _ => controller
        };

        // Try to extract ID from path
        if (segments.Length > 2 && int.TryParse(segments[2], out var id))
        {
            return (entityType, id);
        }

        return (entityType, null);
    }

        private bool ShouldLogRequest(string method, int statusCode)
    {
        return true;
    }
}
