using System.Globalization;
using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.WorldPop;

public sealed record GdalInfo(
    int Width,
    int Height,
    string Wkt,
    double PixelSizeX,
    double PixelSizeY,
    double NoDataValue,
    double Minimum,
    double Maximum,
    double Mean,
    double ValidPercent);

public static class GdalInfoParser
{
    public static GdalInfo Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var size = root.GetProperty("size");
            var geoTransform = root.GetProperty("geoTransform");
            var wkt = root.GetProperty("coordinateSystem").GetProperty("wkt").GetString()
                ?? throw new EtlFailureException("gdalinfo : système de coordonnées illisible.");
            var band = root.GetProperty("bands")[0];
            var validPercent = double.Parse(
                band.GetProperty("metadata").GetProperty("").GetProperty("STATISTICS_VALID_PERCENT")
                    .GetString() ?? "0",
                CultureInfo.InvariantCulture);

            return new GdalInfo(
                Width: size[0].GetInt32(),
                Height: size[1].GetInt32(),
                Wkt: wkt,
                PixelSizeX: geoTransform[1].GetDouble(),
                PixelSizeY: geoTransform[5].GetDouble(),
                NoDataValue: band.GetProperty("noDataValue").GetDouble(),
                Minimum: band.GetProperty("minimum").GetDouble(),
                Maximum: band.GetProperty("maximum").GetDouble(),
                Mean: band.GetProperty("mean").GetDouble(),
                ValidPercent: validPercent);
        }
        catch (JsonException exception)
        {
            throw new EtlFailureException($"gdalinfo : JSON illisible ({exception.Message}).");
        }
        catch (KeyNotFoundException exception)
        {
            throw new EtlFailureException($"gdalinfo : champ manquant ({exception.Message}).");
        }
    }
}
