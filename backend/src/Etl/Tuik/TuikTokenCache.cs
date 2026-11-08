namespace NeighbourhoodExplorer.Etl.Tuik;

public sealed class TuikTokenCache
{
    private static readonly TimeSpan RenewalMargin = TimeSpan.FromSeconds(30);

    private string? accessToken;
    private DateTimeOffset expiresAt;

    public string? Get(DateTimeOffset now) =>
        accessToken is not null && now < expiresAt - RenewalMargin ? accessToken : null;

    public void Store(string token, int expiresInSeconds, DateTimeOffset now)
    {
        accessToken = token;
        expiresAt = now.AddSeconds(expiresInSeconds);
    }
}
