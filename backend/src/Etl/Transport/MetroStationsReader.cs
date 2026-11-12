using System.Globalization;
using System.Text.Json;

namespace NeighbourhoodExplorer.Etl.Transport;

public static class MetroStationsReader
{
    public static TransportReadResult Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("Data", out var data)
            || data.ValueKind != JsonValueKind.Array)
        {
            throw new EtlFailureException($"réponse Metro İstanbul inattendue : pas de tableau « Data » dans {path}.");
        }

        var points = new List<TransportPoint>();
        var rejected = 0;
        foreach (var station in data.EnumerateArray())
        {
            if (!TryReadStation(station, out var point))
            {
                rejected++;
                continue;
            }

            points.Add(point);
        }

        return new TransportReadResult(points, rejected);
    }

    private static bool TryReadStation(JsonElement station, out TransportPoint point)
    {
        point = null!;
        if (!station.TryGetProperty("Name", out var nameElement)
            || nameElement.GetString() is not { Length: > 0 } name
            || !station.TryGetProperty("DetailInfo", out var detail)
            || detail.ValueKind != JsonValueKind.Object
            || !detail.TryGetProperty("Latitude", out var latitudeElement)
            || !detail.TryGetProperty("Longitude", out var longitudeElement)
            || !double.TryParse(
                latitudeElement.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var latitude)
            || !double.TryParse(
                longitudeElement.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var longitude)
            || !TransportCoordinateDecoder.IsInsideIstanbul(longitude, latitude))
        {
            return false;
        }

        point = new TransportPoint(TransportMode.Rail, name.Trim(), longitude, latitude);
        return true;
    }
}
