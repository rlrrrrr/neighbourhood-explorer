using System.Net;
using System.Net.Http.Headers;

namespace NeighbourhoodExplorer.Etl.Tuik;

public sealed class TuikSdmxClient(HttpClient httpClient, TuikTokenProvider tokenProvider)
{
    private static readonly string[] XmlMediaTypes =
    [
        "application/xml",
        "text/xml",
        "application/vnd.sdmx.genericdata+xml",
        "application/vnd.sdmx.structure+xml",
    ];

    public async Task<string> GetAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await tokenProvider.GetTokenAsync(forceRefresh: attempt > 0, cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new EtlFailureException($"TÜİK : délai dépassé pour {url}.");
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new EtlFailureException(
                        $"TÜİK : GET {url} a renvoyé {(int)response.StatusCode} ({response.ReasonPhrase}).");
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!XmlMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new EtlFailureException(
                        $"TÜİK : format reçu inattendu ({mediaType ?? "inconnu"}) pour {url} ; SDMX-ML attendu.");
                }

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
        }

        throw new EtlFailureException("TÜİK : 401 persistant après renouvellement du jeton.");
    }
}
