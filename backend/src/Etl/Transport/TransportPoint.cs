namespace NeighbourhoodExplorer.Etl.Transport;

public sealed record TransportPoint(
    TransportMode Mode,
    string Name,
    double Longitude,
    double Latitude);
