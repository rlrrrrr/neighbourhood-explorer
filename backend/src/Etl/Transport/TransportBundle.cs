namespace NeighbourhoodExplorer.Etl.Transport;

public sealed record TransportBundle(
    IReadOnlyList<TransportPoint> Points,
    IReadOnlyDictionary<string, string> FileChecksums,
    string Checksum,
    TransportReadResult Bus,
    TransportReadResult Rail,
    TransportReadResult Ferry);
