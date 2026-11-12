using System.Globalization;
using System.Text.Json;
using NeighbourhoodExplorer.Etl.Infrastructure;

namespace NeighbourhoodExplorer.Etl.Greenery;

public sealed class WorldCoverRasterPreparer(IExternalToolRunner toolRunner)
{
    private const double MarginDegrees = 0.01;
    private const int ExpectedSrid = 4326;

    public async Task<WorldCoverRasterInfo> PrepareAsync(
        WorldCoverRasterPrepareRequest request,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.OutputDirectory);
        var croppedPath = Path.Combine(
            request.OutputDirectory,
            $"{request.SourceId}-crop.tif");

        var versionResult = await toolRunner.RunAsync(
            new ExternalToolCommand("gdalinfo", ["--version"]),
            cancellationToken);
        if (versionResult.ExitCode != 0)
        {
            throw new EtlFailureException($"gdalinfo a échoué : {versionResult.Tail()}");
        }

        var translate = await toolRunner.RunAsync(
            new ExternalToolCommand(
                "gdal_translate",
                [
                    "-q",
                    "-projwin",
                    Format(request.Bounds.MinLongitude - MarginDegrees),
                    Format(request.Bounds.MaxLatitude + MarginDegrees),
                    Format(request.Bounds.MaxLongitude + MarginDegrees),
                    Format(request.Bounds.MinLatitude - MarginDegrees),
                    "-co",
                    "COMPRESS=LZW",
                    "-co",
                    "TILED=YES",
                    request.RawFilePath,
                    croppedPath,
                ]),
            cancellationToken);
        if (translate.ExitCode != 0)
        {
            throw new EtlFailureException(
                $"gdal_translate a échoué (code {translate.ExitCode}) : {translate.Tail()}");
        }

        var infoResult = await toolRunner.RunAsync(
            new ExternalToolCommand("gdalinfo", ["-json", croppedPath]),
            cancellationToken);
        if (infoResult.ExitCode != 0)
        {
            throw new EtlFailureException(
                $"gdalinfo a échoué sur {croppedPath} : {infoResult.Tail()}");
        }

        using var document = JsonDocument.Parse(infoResult.StandardOutput);
        var root = document.RootElement;
        var size = root.GetProperty("size");
        var geoTransform = root.GetProperty("geoTransform");
        var wkt = root.GetProperty("coordinateSystem").GetProperty("wkt").GetString() ?? string.Empty;
        if (!wkt.Contains($"ID[\"EPSG\",{ExpectedSrid}]", StringComparison.Ordinal))
        {
            throw new EtlFailureException(
                $"le raster découpé n'est pas en EPSG:{ExpectedSrid} " +
                $"(WKT : {wkt[..Math.Min(wkt.Length, 200)]}).");
        }

        return new WorldCoverRasterInfo(
            versionResult.StandardOutput.Trim(),
            croppedPath,
            size[0].GetInt32(),
            size[1].GetInt32(),
            geoTransform[1].GetDouble(),
            geoTransform[5].GetDouble());
    }

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
