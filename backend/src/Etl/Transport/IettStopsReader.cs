namespace NeighbourhoodExplorer.Etl.Transport;

public static class IettStopsReader
{
    public static TransportReadResult Read(string path)
    {
        var points = new List<TransportPoint>();
        var rejected = 0;
        using var reader = new StreamReader(path);

        var headerLine = reader.ReadLine()
            ?? throw new EtlFailureException($"fichier İETT vide : {path}");
        var header = headerLine.TrimStart('\uFEFF').Split(';');
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < header.Length; index++)
        {
            indexes[header[index].Trim()] = index;
        }

        var nameIndex = RequireColumn(indexes, "stop_name", path);
        var latitudeIndex = RequireColumn(indexes, "stop_lat", path);
        var longitudeIndex = RequireColumn(indexes, "stop_lon", path);
        var locationTypeIndex = indexes.GetValueOrDefault("location_type", -1);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split(';');
            if (fields.Length != header.Length)
            {
                rejected++;
                continue;
            }

            var name = fields[nameIndex].Trim();
            var locationType = locationTypeIndex >= 0 ? fields[locationTypeIndex].Trim() : "0";
            if (
                name.Length == 0
                || (locationType.Length > 0 && !string.Equals(locationType, "0", StringComparison.Ordinal))
                || !TransportCoordinateDecoder.TryDecodeIettCoordinate(
                    fields[latitudeIndex],
                    out var latitude)
                || !TransportCoordinateDecoder.TryDecodeIettCoordinate(
                    fields[longitudeIndex],
                    out var longitude)
                || !TransportCoordinateDecoder.IsInsideIstanbul(longitude, latitude))
            {
                rejected++;
                continue;
            }

            points.Add(new TransportPoint(TransportMode.Bus, name, longitude, latitude));
        }

        return new TransportReadResult(points, rejected);
    }

    private static int RequireColumn(
        IReadOnlyDictionary<string, int> indexes,
        string name,
        string path) =>
        indexes.TryGetValue(name, out var index)
            ? index
            : throw new EtlFailureException($"colonne « {name} » absente de {path}.");
}
