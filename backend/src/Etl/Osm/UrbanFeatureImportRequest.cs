using Npgsql;

namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record UrbanFeatureImportRequest(
    NpgsqlDataSource DataSource,
    string RawFilePath,
    UrbanFeatureBoundingBox Bounds,
    string LogFilePath);
