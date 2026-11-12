namespace NeighbourhoodExplorer.Etl.Transport;

public sealed record TransportModeDedupeStats(TransportMode Mode, int Input, int Distinct);

public sealed record TransportDedupeResult(
    IReadOnlyList<TransportPoint> Points,
    IReadOnlyList<TransportModeDedupeStats> Stats);

public static class TransportPointDeduplicator
{
    private const double BusRadiusMetres = 60;
    private const double RailRadiusMetres = 150;
    private const double FerryRadiusMetres = 150;
    private const double GridCellDegrees = 0.002;
    private const double EarthRadiusMetres = 6_371_000;

    public static TransportDedupeResult Deduplicate(IReadOnlyList<TransportPoint> points)
    {
        var distinct = new List<TransportPoint>();
        var stats = new List<TransportModeDedupeStats>();

        foreach (var mode in Enum.GetValues<TransportMode>())
        {
            var modePoints = points
                .Where(point => point.Mode == mode)
                .OrderBy(NormalizeName, StringComparer.Ordinal)
                .ThenBy(point => point.Longitude)
                .ThenBy(point => point.Latitude)
                .ToArray();
            var radius = mode switch
            {
                TransportMode.Bus => BusRadiusMetres,
                TransportMode.Rail => RailRadiusMetres,
                TransportMode.Ferry => FerryRadiusMetres,
                _ => throw new ArgumentOutOfRangeException(nameof(points), mode, null),
            };

            var groups = Cluster(modePoints, radius);
            distinct.AddRange(groups.Select(group => group[0]));
            stats.Add(new TransportModeDedupeStats(mode, modePoints.Length, groups.Count));
        }

        return new TransportDedupeResult(distinct, stats);
    }

    private static List<List<TransportPoint>> Cluster(
        IReadOnlyList<TransportPoint> points,
        double radiusMetres)
    {
        var parent = Enumerable.Range(0, points.Count).ToArray();

        int Find(int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        void Union(int left, int right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot != rightRoot)
            {
                parent[rightRoot] = leftRoot;
            }
        }

        var grid = new Dictionary<(string Name, int Column, int Row), List<int>>();
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var name = NormalizeName(point);
            var column = Cell(point.Longitude);
            var row = Cell(point.Latitude);
            for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
            {
                for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
                {
                    if (!grid.TryGetValue(
                            (name, column + columnOffset, row + rowOffset),
                            out var neighbours))
                    {
                        continue;
                    }

                    foreach (var neighbour in neighbours)
                    {
                        if (DistanceMetres(points[neighbour], point) <= radiusMetres)
                        {
                            Union(index, neighbour);
                        }
                    }
                }
            }

            var key = (name, column, row);
            if (!grid.TryGetValue(key, out var bucket))
            {
                bucket = [];
                grid[key] = bucket;
            }

            bucket.Add(index);
        }

        var groups = new SortedDictionary<int, List<TransportPoint>>();
        for (var index = 0; index < points.Count; index++)
        {
            var root = Find(index);
            if (!groups.TryGetValue(root, out var group))
            {
                group = [];
                groups[root] = group;
            }

            group.Add(points[index]);
        }

        return groups.Values.ToList();
    }

    private static int Cell(double degrees) => (int)Math.Floor(degrees / GridCellDegrees);

    private static double DistanceMetres(TransportPoint left, TransportPoint right)
    {
        var latitude1 = DegreesToRadians(left.Latitude);
        var latitude2 = DegreesToRadians(right.Latitude);
        var latitudeDelta = latitude2 - latitude1;
        var longitudeDelta = DegreesToRadians(right.Longitude - left.Longitude);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(latitude1) * Math.Cos(latitude2) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return 2 * EarthRadiusMetres * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static string NormalizeName(TransportPoint point) =>
        TurkishText.Normalize(
            point.Mode == TransportMode.Ferry
                ? StripTrailingNumber(point.Name)
                : point.Name);

    private static string StripTrailingNumber(string value)
    {
        var end = value.Length;
        while (end > 0 && char.IsAsciiDigit(value[end - 1]))
        {
            end--;
        }

        return end > 0 && end < value.Length && value[end - 1] == '-'
            ? value[..(end - 1)].TrimEnd()
            : value;
    }
}
