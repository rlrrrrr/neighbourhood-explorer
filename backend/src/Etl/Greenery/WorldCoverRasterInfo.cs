namespace NeighbourhoodExplorer.Etl.Greenery;

public sealed record WorldCoverRasterInfo(
    string GdalVersion,
    string CroppedPath,
    int Width,
    int Height,
    double PixelSizeX,
    double PixelSizeY);
