using System.Text;

namespace ElectionApi.Net.Middleware;

public class SwaggerAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _username;
    private readonly string _password;

    public SwaggerAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _username = configuration["SwaggerAuth:Username"] ?? "lohran";
        _password = configuration["SwaggerAuth:Password"] ?? "123456";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only protect swagger endpoints
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            string authHeader = context.Request.Headers["Authorization"];
            
            if (authHeader != null && authHeader.StartsWith("Basic "))
            {
                // Get the encoded username and password
                var encodedUsernamePassword = authHeader.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[1]?.Trim();
                
                // Decode from Base64 to string
                var decodedUsernamePassword = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUsernamePassword));
                
                // Split username and password
                var username = decodedUsernamePassword.Split(':', 2)[0];
                var password = decodedUsernamePassword.Split(':', 2)[1];
                
                // Check if login is correct
                if (IsAuthorized(username, password))
                {
                    await _next.Invoke(context);
                    return;
                }
            }
            
            // Return authentication type (causes browser to show login dialog)
            context.Response.Headers["WWW-Authenticate"] = "Basic";
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Swagger access requires authentication");
            return;
        }

        await _next.Invoke(context);
    }

    public bool IsAuthorized(string username, string password)
    {
        // Check that both the username and password are correct
        return username.Equals(_username, StringComparison.InvariantCultureIgnoreCase)
               && password.Equals(_password);
    }
}