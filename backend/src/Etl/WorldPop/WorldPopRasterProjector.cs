using System.Globalization;
using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.WorldPop;

public sealed record WorldPopBounds(double MinX, double MinY, double MaxX, double MaxY);

public sealed record WorldPopRasterInfo(
    string GdalVersion,
    string ProjectedPath,
    int Width,
    int Height,
    int Srid,
    double PixelSizeX,
    double PixelSizeY,
    double NoDataValue,
    double Minimum,
    double Maximum,
    double Mean,
    double ValidPercent,
    long ValidCellCount);

public sealed record WorldPopProjectionRequest(
    string SourceId,
    string RawFilePath,
    string OutputDirectory,
    WorldPopBounds Bounds);

public static class WorldPopRasterProjector
{
    private const double MarginMetres = 1000;

    public static async Task<WorldPopRasterInfo> ProjectAsync(
        WorldPopProjectionRequest request,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.OutputDirectory);
        var projectedPath = Path.Combine(request.OutputDirectory, $"{request.SourceId}-epsg32635.tif");

        var versionResult = await WorldPopCommandRunner.RunAsync(
            new WorldPopCommand("gdalinfo", ["--version"]),
            cancellationToken);
        if (versionResult.ExitCode != 0)
        {
            throw new EtlFailureException(
                $"gdalinfo a échoué : {WorldPopCommandRunner.Tail(versionResult.StandardError)}");
        }

        var gdalVersion = versionResult.StandardOutput.Trim();
        var bounds = request.Bounds;
        var warp = await WorldPopCommandRunner.RunAsync(
            new WorldPopCommand(
                "gdalwarp",
                [
                    "-overwrite",
                    "-t_srs",
                    "EPSG:32635",
                    "-te",
                    Format(bounds.MinX - MarginMetres),
                    Format(bounds.MinY - MarginMetres),
                    Format(bounds.MaxX + MarginMetres),
                    Format(bounds.MaxY + MarginMetres),
                    "-tr",
                    "100",
                    "100",
                    "-r",
                    "sum",
                    "-srcnodata",
                    "-99999",
                    "-dstnodata",
                    "-99999",
                    "-co",
                    "COMPRESS=LZW",
                    "-co",
                    "TILED=YES",
                    request.RawFilePath,
                    projectedPath,
                ]),
            cancellationToken);

        if (warp.ExitCode != 0)
        {
            throw new EtlFailureException(
                $"gdalwarp a échoué (code {warp.ExitCode}) : {WorldPopCommandRunner.Tail(warp.StandardError)}");
        }

        var info = await WorldPopCommandRunner.RunAsync(
            new WorldPopCommand("gdalinfo", ["-json", "-stats", projectedPath]),
            cancellationToken);
        if (info.ExitCode != 0)
        {
            throw new EtlFailureException(
                $"gdalinfo a échoué sur {projectedPath} : {WorldPopCommandRunner.Tail(info.StandardError)}");
        }

        using var document = JsonDocument.Parse(info.StandardOutput);
        var root = document.RootElement;
        var size = root.GetProperty("size");
        var width = size[0].GetInt32();
        var height = size[1].GetInt32();
        var geoTransform = root.GetProperty("geoTransform");
        var pixelSizeX = geoTransform[1].GetDouble();
        var pixelSizeY = geoTransform[5].GetDouble();
        var wkt = root.GetProperty("coordinateSystem").GetProperty("wkt").GetString() ?? string.Empty;
        if (!wkt.Contains("ID[\"EPSG\",32635]", StringComparison.Ordinal))
        {
            throw new EtlFailureException(
                $"le raster projeté n'est pas en EPSG:32635 (WKT : {wkt[..Math.Min(wkt.Length, 200)]}).");
        }

        var band = root.GetProperty("bands")[0];
        var noDataValue = band.GetProperty("noDataValue").GetDouble();
        var minimum = band.GetProperty("minimum").GetDouble();
        var maximum = band.GetProperty("maximum").GetDouble();
        var mean = band.GetProperty("mean").GetDouble();
        var validPercent = double.Parse(
            band.GetProperty("metadata").GetProperty("").GetProperty("STATISTICS_VALID_PERCENT").GetString()
                ?? "0",
            CultureInfo.InvariantCulture);

        return new WorldPopRasterInfo(
            gdalVersion,
            projectedPath,
            width,
            height,
            32635,
            pixelSizeX,
            pixelSizeY,
            noDataValue,
            minimum,
            maximum,
            mean,
            validPercent,
            (long)Math.Round(width * (double)height * validPercent / 100));
    }

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
