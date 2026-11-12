using System.Globalization;
using NeighbourhoodExplorer.Etl.Data;
using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Manifests;
using NeighbourhoodExplorer.Etl.Osm;
using NeighbourhoodExplorer.Etl.Reports;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed class OsmUrbanFeaturesPipeline(
    NpgsqlDataSource dataSource,
    OsmDatabaseTarget target,
    IExternalToolRunner toolRunner,
    IEtlConsole console,
    TimeProvider timeProvider) : IEtlPipeline
{
    private const double BoundingBoxMarginDegrees = 0.02;

    public PipelineId Id => PipelineId.OsmUrbanFeatures;

    public async Task<EtlOutcome> RunAsync(
        EtlPipelineRequest request,
        CancellationToken cancellationToken)
    {
        var expectedNeighborhoods = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Neighborhoods,
            request.Manifest.Id);
        var expectedDistricts = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Districts,
            request.Manifest.Id);
        var observedAt = ParseReferenceDate(request.Manifest);
        var retrievedAt = ParseRetrievedAt(request.Manifest);

        var rawSourceId = request.Manifest.RawSourceId ?? request.Manifest.Id;
        var dataSourceId = request.Manifest.DataSourceId ?? request.Manifest.Id;
        var rawFile = SourceFileLocator.FindSingle(
            request.DataDirectory,
            rawSourceId,
            "*.osm.pbf");
        var checksum = await Sha256Hasher.ComputeFileAsync(rawFile, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Manifest.ChecksumSha256)
            && !string.Equals(
                checksum,
                request.Manifest.ChecksumSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new EtlFailureException(
                $"SHA-256 du fichier brut ({checksum}) différent de celui du manifeste " +
                $"({request.Manifest.ChecksumSha256}).");
        }

        var bounds = (await UrbanFeatureBoundingBoxReader.ReadAsync(dataSource, cancellationToken))
            .Expand(BoundingBoxMarginDegrees);
        var osmImport = new OsmUrbanFeaturesImport(target, toolRunner, console);
        var osmVersion = await osmImport.RunAsync(
            new UrbanFeatureImportRequest(
                dataSource,
                rawFile,
                bounds,
                Path.Combine(
                    request.DataDirectory,
                    "processed",
                    request.Manifest.Id,
                    "osm2pgsql.log")),
            cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var counts = await UrbanFeatureCountReader.ReadAsync(
            connection,
            transaction,
            cancellationToken);

        console.WriteLine(
            $"ETL: objets urbains OSM : {counts.Staging.PoiRows:N0} POI, " +
            $"{counts.Staging.AreaRows:N0} surfaces, {counts.Staging.HighwayRows:N0} tronçons.");

        var metrics = UrbanFeatureMetricCalculator.Calculate(counts.Neighborhoods);
        var failures = new List<string>();
        AppendImportFailures(counts.Staging, failures);
        new CoverageScope
        {
            NeighborhoodCount = counts.Neighborhoods.Count,
            DistrictCount = counts.Staging.DistrictCount,
            ExpectedNeighborhoods = expectedNeighborhoods,
            ExpectedDistricts = expectedDistricts,
        }.AppendFailures(failures);
        foreach (var series in metrics.Series)
        {
            new MetricCoverageCheck
            {
                MetricKey = series.Definition.Key,
                ExpectedValues = expectedNeighborhoods,
                Values = series.Values,
            }.AppendFailures(failures);
        }

        var computedAt = timeProvider.GetUtcNow();
        var metricWrite = await MetricSeriesWriter.WriteAsync(
            new MetricSeriesWriteRequest(
                connection,
                transaction,
                new MetricCatalogSource(
                    dataSourceId,
                    request.Manifest.Name,
                    request.Manifest.Organization,
                    request.Manifest.Url,
                    request.Manifest.License,
                    request.Manifest.AttributionText,
                    UrbanFeatureMetricCatalog.SourceMethodology,
                    retrievedAt,
                    request.Manifest.SourceVersion ?? string.Empty),
                request.Manifest.ReferencePeriod,
                observedAt,
                computedAt,
                metrics.Series),
            cancellationToken);

        var report = new UrbanFeatureReport(
            request.Manifest.Id,
            dataSourceId,
            request.Manifest.Name,
            request.Manifest.Organization,
            request.Manifest.Url,
            request.Manifest.License,
            request.Manifest.AttributionText,
            request.Manifest.SourceVersion ?? string.Empty,
            checksum,
            osmVersion,
            request.Manifest.ReferencePeriod,
            observedAt,
            computedAt,
            bounds,
            counts.Neighborhoods.Count,
            counts.Staging.DistrictCount,
            expectedNeighborhoods,
            expectedDistricts,
            counts.Staging,
            counts.PoiCategories,
            counts.Neighborhoods,
            metrics.Series,
            failures);

        await UrbanFeatureReportWriter.WriteAsync(
            request.DataDirectory,
            report,
            cancellationToken);

        var success = failures.Count == 0;
        if (success)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        console.WriteLine(
            $"ETL: rapport des objets urbains OSM : " +
            $"{Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "urban-features-check.md")}");

        return new EtlOutcome(
            success,
            checksum,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["neighborhoods"] = counts.Neighborhoods.Count,
                ["districts"] = counts.Staging.DistrictCount,
                ["poi"] = counts.Staging.PoiRows,
                ["areas"] = counts.Staging.AreaRows,
                ["highways"] = counts.Staging.HighwayRows,
                ["metrics"] = metricWrite.MetricIds.Count,
                ["metric_values"] = metrics.Series.Sum(series => series.Values.Count),
            },
            success ? null : string.Join(" ; ", failures));
    }

    private static DateOnly ParseReferenceDate(SourceManifest manifest) =>
        DateOnly.TryParse(
            manifest.ReferencePeriod,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var observedAt)
            ? observedAt
            : throw new EtlFailureException(
                $"reference_period illisible dans le manifeste : « {manifest.ReferencePeriod} ».");

    private static DateOnly ParseRetrievedAt(SourceManifest manifest) =>
        DateOnly.TryParse(
            manifest.RetrievedAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var retrievedAt)
            ? retrievedAt
            : throw new EtlFailureException(
                $"retrieved_at illisible dans le manifeste : « {manifest.RetrievedAt} ».");

    private static void AppendImportFailures(UrbanFeatureStagingCounts staging, List<string> failures)
    {
        if (staging.PoiRows == 0)
        {
            failures.Add("import OSM : aucun POI dans osm_urban_poi.");
        }

        if (staging.AreaRows == 0)
        {
            failures.Add("import OSM : aucune surface dans osm_urban_area.");
        }

        if (staging.HighwayRows == 0)
        {
            failures.Add("import OSM : aucun tronçon dans osm_urban_highway.");
        }
    }
}
