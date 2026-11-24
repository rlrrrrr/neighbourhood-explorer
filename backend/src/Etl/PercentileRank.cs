namespace NeighbourhoodExplorer.Etl;

public static class PercentileRank
{
    public static IReadOnlyList<double> Normalize(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return [];
        }

        if (IsZeroInflated(values))
        {
            return NormalizePositiveValues(values);
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

            SetTieGroupRank(normalized, sortedIndices, start, end, value);

            start = end + 1;
        }

        return normalized;
    }

    // Sur une métrique où la plupart des quartiers valent exactement zéro (vie
    // nocturne, culture), le rang percentile global donne au plus petit positif le
    // rang de tous les zéros réunis : un lieu presque vide hérite du haut de la rampe.
    // Les zéros restent donc à 0 et les valeurs positives se classent entre elles :
    // le plus petit positif vaut 1/k, le plus grand vaut 1 (k = nombre de positifs).
    private static bool IsZeroInflated(IReadOnlyList<double> values)
    {
        var hasZero = false;
        var hasPositive = false;
        foreach (var value in values)
        {
            if (value < 0)
            {
                return false;
            }

            if (value == 0)
            {
                hasZero = true;
            }
            else
            {
                hasPositive = true;
            }
        }

        return hasZero && hasPositive;
    }

    private static double[] NormalizePositiveValues(IReadOnlyList<double> values)
    {
        var normalized = new double[values.Count];
        var positiveIndices = Enumerable.Range(0, values.Count)
            .Where(index => values[index] > 0)
            .OrderBy(index => values[index])
            .ToArray();

        var start = 0;
        while (start < positiveIndices.Length)
        {
            var end = start;
            while (end + 1 < positiveIndices.Length
                && values[positiveIndices[end + 1]] == values[positiveIndices[start]])
            {
                end++;
            }

            var rank = (start + 1) / (double)positiveIndices.Length;
            SetTieGroupRank(normalized, positiveIndices, start, end, rank);

            start = end + 1;
        }

        return normalized;
    }

    private static void SetTieGroupRank(
        double[] normalized,
        IReadOnlyList<int> sortedIndices,
        int start,
        int end,
        double value)
    {
        for (var index = start; index <= end; index++)
        {
            normalized[sortedIndices[index]] = value;
        }
    }
}
