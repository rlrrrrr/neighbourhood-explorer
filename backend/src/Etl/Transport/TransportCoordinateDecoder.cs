using System.Globalization;
using System.Text;

namespace NeighbourhoodExplorer.Etl.Transport;

public static class TransportCoordinateDecoder
{
    private const double MinimumLatitude = 40.5;
    private const double MaximumLatitude = 41.8;
    private const double MinimumLongitude = 27.5;
    private const double MaximumLongitude = 30.2;

    public static bool TryDecodeIettCoordinate(string raw, out double value)
    {
        value = 0;
        if (raw.Contains(',', StringComparison.Ordinal))
        {
            return false;
        }

        var digits = new StringBuilder(raw.Length);
        foreach (var character in raw)
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
            }
        }

        if (digits.Length < 8)
        {
            return false;
        }

        value = double.Parse(digits.ToString(), CultureInfo.InvariantCulture)
            / Math.Pow(10, digits.Length - 2);
        return double.IsFinite(value);
    }

    public static bool IsInsideIstanbul(double longitude, double latitude) =>
        latitude >= MinimumLatitude
        && latitude <= MaximumLatitude
        && longitude >= MinimumLongitude
        && longitude <= MaximumLongitude;
}
