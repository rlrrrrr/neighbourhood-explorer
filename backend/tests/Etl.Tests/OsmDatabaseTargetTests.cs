using NeighbourhoodExplorer.Etl.Osm;
using Xunit;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class OsmDatabaseTargetTests
{
    [Fact]
    public void Conserve_le_mot_de_passe_pour_osm2pgsql()
    {
        var target = OsmDatabaseTarget.FromConnectionString(
            "Host=db;Port=5432;Database=explorer;Username=explorer;Password=s3cret");

        Assert.Equal("db", target.Host);
        Assert.Equal(5432, target.Port);
        Assert.Equal("explorer", target.Database);
        Assert.Equal("explorer", target.Username);
        Assert.Equal("s3cret", target.Password);
    }

    [Fact]
    public void Refuse_une_connexion_sans_hote()
    {
        var exception = Assert.Throws<EtlFailureException>(
            () => OsmDatabaseTarget.FromConnectionString("Database=explorer;Username=explorer"));

        Assert.Contains("Host", exception.Message, StringComparison.Ordinal);
    }
}
