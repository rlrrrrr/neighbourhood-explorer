using NeighbourhoodExplorer.Etl.Data;

namespace NeighbourhoodExplorer.Etl.Transport;

public static class TransportBundleReader
{
    public const string IettStopsFile = "iett-stops.csv";
    public const string MetroStationsFile = "metro-stations.json";
    public const string FerryPiersFile = "sehir-hatlari-piers.kml";

    private static readonly string[] FileNames =
    [
        IettStopsFile,
        MetroStationsFile,
        FerryPiersFile,
    ];

    public static async Task<TransportBundle> ReadAsync(
        string dataDirectory,
        string sourceId,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(dataDirectory, "raw", sourceId);
        if (!Directory.Exists(directory))
        {
            throw new EtlFailureException($"répertoire de données brutes introuvable : {directory}");
        }

        var checksums = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fileName in FileNames)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                throw new EtlFailureException(
                    $"fichier brut manquant pour « {sourceId} » : {path} " +
                    "(attendu : iett-stops.csv, metro-stations.json, sehir-hatlari-piers.kml).");
            }

            checksums[fileName] = await Sha256Hasher.ComputeFileAsync(path, cancellationToken);
        }

        var bus = IettStopsReader.Read(Path.Combine(directory, IettStopsFile));
        var rail = MetroStationsReader.Read(Path.Combine(directory, MetroStationsFile));
        var ferry = FerryPiersReader.Read(Path.Combine(directory, FerryPiersFile));

        var points = new List<TransportPoint>(bus.Points.Count + rail.Points.Count + ferry.Points.Count);
        points.AddRange(bus.Points);
        points.AddRange(rail.Points);
        points.AddRange(ferry.Points);

        return new TransportBundle(
            points,
            checksums,
            TransportBundleChecksum.Compute(checksums),
            bus,
            rail,
            ferry);
    }
}
