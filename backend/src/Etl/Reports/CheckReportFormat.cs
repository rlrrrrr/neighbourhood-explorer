using System.Globalization;

namespace NeighbourhoodExplorer.Etl.Reports;

public static class CheckReportFormat
{
    public static string Number(double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2
            : values[middle];
    }

    public static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);

    public static string CsvField(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
