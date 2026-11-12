using System.Globalization;
using System.Xml.Linq;

namespace NeighbourhoodExplorer.Etl.Transport;

public static class FerryPiersReader
{
    public static TransportReadResult Read(string path)
    {
        var document = XDocument.Load(path);
        var points = new List<TransportPoint>();
        var rejected = 0;

        foreach (var placemark in document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "Placemark", StringComparison.Ordinal)))
        {
            var name = placemark
                .Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "name", StringComparison.Ordinal))
                ?.Value.Trim();
            var coordinates = placemark
                .Descendants()
                .FirstOrDefault(element => string.Equals(
                    element.Name.LocalName,
                    "coordinates",
                    StringComparison.Ordinal))
                ?.Value.Trim();

            if (string.IsNullOrEmpty(name)
                || string.IsNullOrEmpty(coordinates)
                || !TryReadCoordinates(coordinates, out var longitude, out var latitude))
            {
                rejected++;
                continue;
            }

            points.Add(new TransportPoint(TransportMode.Ferry, name, longitude, latitude));
        }

        return new TransportReadResult(points, rejected);
    }

    private static bool TryReadCoordinates(
        string raw,
        out double longitude,
        out double latitude)
    {
        longitude = 0;
        latitude = 0;
        var first = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        var parts = first.Split(',');
        return parts.Length >= 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            && TransportCoordinateDecoder.IsInsideIstanbul(longitude, latitude);
    }
}
