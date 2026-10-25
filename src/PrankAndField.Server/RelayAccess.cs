using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace PrankAndField.Server;

/// <summary>
/// Simple gate in front of the hub: a shared access key (when <see cref="RelayOptions.AccessKey"/> is set)
/// and, for browsers, an allowed Origin. CORS alone does not stop WebSockets from other sites.
/// </summary>
public static class RelayAccess
{
    public static IApplicationBuilder UseRelayAccess(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments(MatchHub.Path))
        {
            var options = context.RequestServices.GetRequiredService<IOptions<RelayOptions>>().Value;
            int status = Check(context.Request, options);
            if (status != StatusCodes.Status200OK)
            {
                context.Response.StatusCode = status;
                return;
            }
        }
        await next(context);
    });

    /// <summary>200 when the request may reach the hub, otherwise the status to answer with.</summary>
    public static int Check(HttpRequest request, RelayOptions options)
    {
        string origin = request.Headers.Origin.ToString();
        if (origin.Length > 0 && options.AllowedOrigins.Length > 0
            && !options.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return StatusCodes.Status403Forbidden;
        }

        if (string.IsNullOrEmpty(options.AccessKey)) return StatusCodes.Status200OK;
        // Browsers cannot set headers on a WebSocket, so SignalR sends the token in the query string there.
        string? token = request.Query["access_token"];
        string auth = request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(token) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) token = auth["Bearer ".Length..].Trim();
        return token is not null && KeysMatch(token, options.AccessKey) ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized;
    }

    private static bool KeysMatch(string given, string expected) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
