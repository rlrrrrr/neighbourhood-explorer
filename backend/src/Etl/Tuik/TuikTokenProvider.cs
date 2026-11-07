using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NeighbourhoodExplorer.Etl.Tuik;

public sealed class TuikTokenProvider(HttpClient httpClient, string apiKey, TuikTokenCache cache)
{
    public const string Endpoint = "https://giris.tuik.gov.tr/realms/web/protocol/openid-connect/token";

    public async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && cache.Get(DateTimeOffset.UtcNow) is { } cached)
        {
            return cached;
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "nsi-ws-consumer",
            ["api_key"] = apiKey,
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(Endpoint, content, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EtlFailureException("TÜİK : délai dépassé pour l'obtention du jeton.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new EtlFailureException($"TÜİK : échec du jeton ({response.StatusCode}).");
            }

            var payload = await response.Content.ReadFromJsonAsync<TuikTokenResponse>(cancellationToken)
                ?? throw new EtlFailureException("TÜİK : réponse de jeton illisible.");

            cache.Store(payload.AccessToken, payload.ExpiresIn, DateTimeOffset.UtcNow);
            return payload.AccessToken;
        }
    }

    private sealed record TuikTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
