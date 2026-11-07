using System.Security.Cryptography;
using System.Text.Json;

namespace NeighbourhoodExplorer.Api.Http;

public static class CachedJsonResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IResult Create<T>(
        HttpContext context,
        T payload,
        int maxAgeSeconds = 300,
        string contentType = "application/json; charset=utf-8")
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var etag = $"\"{Convert.ToHexStringLower(SHA256.HashData(bytes))}\"";
        context.Response.Headers.ETag = etag;
        context.Response.Headers.CacheControl = $"public, max-age={maxAgeSeconds}";

        if (MatchesIfNoneMatch(context, etag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Bytes(bytes, contentType);
    }

    private static bool MatchesIfNoneMatch(HttpContext context, string etag) =>
        context.Request.Headers.IfNoneMatch
            .SelectMany(value => (value ?? string.Empty).Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(candidate => string.Equals(candidate, etag, StringComparison.Ordinal));
}
