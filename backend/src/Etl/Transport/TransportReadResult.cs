namespace NeighbourhoodExplorer.Etl.Transport;

public sealed record TransportReadResult(IReadOnlyList<TransportPoint> Points, int Rejected);
