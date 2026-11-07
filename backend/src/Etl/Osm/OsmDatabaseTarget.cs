using Npgsql;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record OsmDatabaseTarget(string Host, int Port, string Database, string Username, string Password)
{
    public static OsmDatabaseTarget FromConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        var host = builder.Host
            ?? throw new EtlFailureException("DATABASE_CONNECTION_STRING sans « Host ».");
        var database = builder.Database
            ?? throw new EtlFailureException("DATABASE_CONNECTION_STRING sans « Database ».");
        var username = builder.Username
            ?? throw new EtlFailureException("DATABASE_CONNECTION_STRING sans « Username ».");

        return new OsmDatabaseTarget(host, builder.Port, database, username, builder.Password ?? string.Empty);
    }
}
