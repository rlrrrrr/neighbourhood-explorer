namespace NeighbourhoodExplorer.Etl.Greenery;

public sealed record WorldCoverBounds(
    double MinLongitude,
    double MinLatitude,
    double MaxLongitude,
    double MaxLatitude);
