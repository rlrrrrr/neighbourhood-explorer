namespace NeighbourhoodExplorer.Etl.Database;

public static class MetricSeriesWriter
{
    public static async Task<MetricSeriesWriteResult> WriteAsync(
        MetricSeriesWriteRequest request,
        CancellationToken cancellationToken)
    {
        var metricIds = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var series in request.Series)
        {
            var definition = series.Definition;
            var metricId = await MetricCatalogWriter.UpsertAsync(
                new MetricCatalogWriteRequest(
                    request.Connection,
                    request.Transaction,
                    request.Source,
                    definition),
                cancellationToken);
            metricIds[definition.Key] = metricId;

            await MetricValueWriter.WriteAsync(
                new MetricValueWriteRequest(
                    request.Connection,
                    request.Transaction,
                    metricId,
                    request.ObservedAt,
                    request.ReferencePeriod,
                    definition.QualityFlag,
                    request.ComputedAt,
                    series.Values),
                cancellationToken);
        }

        return new MetricSeriesWriteResult(metricIds);
    }
}
