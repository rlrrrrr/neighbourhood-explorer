using NeighbourhoodExplorer.Etl.Tuik;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TuikTokenCacheTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Get_returns_null_before_any_token_is_stored()
    {
        var cache = new TuikTokenCache();

        Assert.Null(cache.Get(Now));
    }

    [Fact]
    public void Get_returns_the_token_until_thirty_seconds_before_expiry()
    {
        var cache = new TuikTokenCache();
        cache.Store("jeton", 300, Now);

        Assert.Equal("jeton", cache.Get(Now.AddSeconds(269)));
        Assert.Null(cache.Get(Now.AddSeconds(270)));
    }

    [Fact]
    public void Store_replaces_the_previous_token()
    {
        var cache = new TuikTokenCache();
        cache.Store("ancien", 300, Now);
        cache.Store("nouveau", 300, Now.AddSeconds(200));

        Assert.Equal("nouveau", cache.Get(Now.AddSeconds(250)));
    }
}
