using CustomAppTemplate.Models;
using Microsoft.Extensions.Options;

namespace CustomAppTemplate.Middleware;

public class AuthMiddleware(IOptions<AuthOptions> options) : IMiddleware
{
    private readonly AuthOptions _auth = options.Value;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!_auth.Enabled)
        {
            await next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authHeader))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Authorization header missing" });
            return;
        }

        var parts = authHeader.Split(' ', 2);

        if (parts.Length != 2 || parts[0] != "Bearer" || string.IsNullOrEmpty(parts[1]))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid authorization format. Use: Bearer TOKEN" });
            return;
        }

        if (parts[1] != _auth.ApiKey)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        await next(context);
    }
}
