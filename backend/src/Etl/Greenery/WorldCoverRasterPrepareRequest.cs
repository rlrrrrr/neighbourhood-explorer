namespace NeighbourhoodExplorer.Etl.Greenery;

public sealed record WorldCoverRasterPrepareRequest(
    string SourceId,
    string RawFilePath,
    string OutputDirectory,
    WorldCoverBounds Bounds);
