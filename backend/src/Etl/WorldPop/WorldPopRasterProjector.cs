using System.Globalization;
using NeighbourhoodExplorer.Etl.Infrastructure;

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

public sealed class WorldPopRasterProjector(IExternalToolRunner toolRunner)
{
    private const double MarginMetres = 1000;
    private const int TargetSrid = 32635;

    public async Task<WorldPopRasterInfo> ProjectAsync(
        WorldPopProjectionRequest request,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.OutputDirectory);
        var projectedPath = Path.Combine(request.OutputDirectory, $"{request.SourceId}-epsg32635.tif");

        var versionResult = await toolRunner.RunAsync(
            new ExternalToolCommand("gdalinfo", ["--version"]),
            cancellationToken);
        if (versionResult.ExitCode != 0)
        {
            throw new EtlFailureException($"gdalinfo a échoué : {versionResult.Tail()}");
        }

        var gdalVersion = versionResult.StandardOutput.Trim();
        var bounds = request.Bounds;
        var warp = await toolRunner.RunAsync(
            new ExternalToolCommand(
                "gdalwarp",
                [
                    "-overwrite",
                    "-t_srs",
                    $"EPSG:{TargetSrid}",
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
                $"gdalwarp a échoué (code {warp.ExitCode}) : {warp.Tail()}");
        }

        var infoResult = await toolRunner.RunAsync(
            new ExternalToolCommand("gdalinfo", ["-json", "-stats", projectedPath]),
            cancellationToken);
        if (infoResult.ExitCode != 0)
        {
            throw new EtlFailureException(
                $"gdalinfo a échoué sur {projectedPath} : {infoResult.Tail()}");
        }

        var info = GdalInfoParser.Parse(infoResult.StandardOutput);
        if (!info.Wkt.Contains($"ID[\"EPSG\",{TargetSrid}]", StringComparison.Ordinal))
        {
            throw new EtlFailureException(
                $"le raster projeté n'est pas en EPSG:{TargetSrid} " +
                $"(WKT : {info.Wkt[..Math.Min(info.Wkt.Length, 200)]}).");
        }

        return new WorldPopRasterInfo(
            gdalVersion,
            projectedPath,
            info.Width,
            info.Height,
            TargetSrid,
            info.PixelSizeX,
            info.PixelSizeY,
            info.NoDataValue,
            info.Minimum,
            info.Maximum,
            info.Mean,
            info.ValidPercent,
            (long)Math.Round(info.Width * (double)info.Height * info.ValidPercent / 100));
    }

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
