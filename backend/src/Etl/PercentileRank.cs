namespace NeighbourhoodExplorer.Etl;

public static class PercentileRank
{
    public static IReadOnlyList<double> Normalize(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return [];
        }

        var sortedIndices = Enumerable.Range(0, values.Count)
            .OrderBy(index => values[index])
            .ToArray();

        var normalized = new double[values.Count];
        var start = 0;
        while (start < sortedIndices.Length)
        {
            var end = start;
            while (end + 1 < sortedIndices.Length
                && values[sortedIndices[end + 1]] == values[sortedIndices[start]])
            {
                end++;
            }

            var rank = start + 1;
            var value = sortedIndices.Length == 1
                ? 0
                : (rank - 1) / (double)(sortedIndices.Length - 1);

            for (var index = start; index <= end; index++)
            {
                normalized[sortedIndices[index]] = value;
            }

            start = end + 1;
        }

        return normalized;
    }
}
