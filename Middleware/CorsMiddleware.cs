namespace ElectionApi.Net.Middleware;

public class CorsMiddleware
{
    private readonly RequestDelegate _next;

    public CorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers["Origin"].FirstOrDefault();
        
        // Configure CORS headers
        context.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
        
        if (!string.IsNullOrEmpty(origin))
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", origin);
        }
        else
        {
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        }
        
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, PATCH, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", 
            "Content-Type, Authorization, X-Requested-With, Accept, Origin, Access-Control-Request-Method, Access-Control-Request-Headers");
        context.Response.Headers.Add("Access-Control-Max-Age", "86400"); // 24 hours

        // Handle preflight requests
        if (context.Request.Method == "OPTIONS")
        {
            context.Response.StatusCode = 200;
            return;
        }

        await _next(context);
    }
}