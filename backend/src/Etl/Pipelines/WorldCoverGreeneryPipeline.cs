using System.Globalization;
using NeighbourhoodExplorer.Etl.Data;
using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Greenery;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Manifests;
using NeighbourhoodExplorer.Etl.Osm;
using NeighbourhoodExplorer.Etl.Reports;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed class WorldCoverGreeneryPipeline(
    NpgsqlDataSource dataSource,
    OsmDatabaseTarget target,
    IExternalToolRunner toolRunner,
    IEtlConsole console,
    TimeProvider timeProvider) : IEtlPipeline
{
    public PipelineId Id => PipelineId.WorldCoverGreenery;

    public async Task<EtlOutcome> RunAsync(
        EtlPipelineRequest request,
        CancellationToken cancellationToken)
    {
        var referenceYear = ParseReferenceYear(request.Manifest);
        var expectedNeighborhoods = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Neighborhoods,
            request.Manifest.Id);
        var expectedDistricts = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Districts,
            request.Manifest.Id);
        var dataSourceId = request.Manifest.DataSourceId ?? request.Manifest.Id;

        var rawSourceId = request.Manifest.RawSourceId ?? request.Manifest.Id;
        var rawFile = SourceFileLocator.FindSingle(
            request.DataDirectory,
            rawSourceId,
            "*.tif");
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

        var bounds = await WorldCoverBoundsReader.ReadAsync(dataSource, cancellationToken);
        var preparer = new WorldCoverRasterPreparer(toolRunner);
        var raster = await preparer.PrepareAsync(
            new WorldCoverRasterPrepareRequest(
                request.Manifest.Id,
                rawFile,
                Path.Combine(request.DataDirectory, "processed", request.Manifest.Id),
                bounds),
            cancellationToken);

        console.WriteLine(
            $"ETL: WorldCover découpé en EPSG:4326 " +
            $"({raster.Width:N0} × {raster.Height:N0} pixels).");

        var loader = new WorldCoverRasterLoader(dataSource, toolRunner, console);
        var loaded = await loader.LoadAsync(
            new WorldCoverRasterLoadRequest(target, raster.CroppedPath),
            cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var counts = await GreeneryZonalCounter.CountAsync(
            connection,
            transaction,
            cancellationToken);
        var metrics = GreeneryMetricCalculator.Calculate(counts);
        var districtCount = counts.Select(count => count.DistrictName).Distinct().Count();

        console.WriteLine(
            $"ETL: WorldCover : {counts.Count} mahalles, {districtCount} ilçe, " +
            $"{counts.Count(count => GreeneryMetricCalculator.Share(count) == 0)} " +
            $"part(s) nulle(s).");

        var computedAt = timeProvider.GetUtcNow();
        var observedAt = new DateOnly(referenceYear, 12, 31);
        var series = metrics.Series[0];
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
                    GreeneryMetricCatalog.SourceMethodology,
                    DateOnly.Parse(
                        request.Manifest.RetrievedAt!,
                        CultureInfo.InvariantCulture),
                    request.Manifest.SourceVersion ?? string.Empty),
                request.Manifest.ReferencePeriod,
                observedAt,
                computedAt,
                metrics.Series),
            cancellationToken);

        var neighborhoods = new List<GreeneryNeighborhoodResult>(counts.Count);
        for (var index = 0; index < counts.Count; index++)
        {
            var count = counts[index];
            var value = series.Values[index];
            neighborhoods.Add(new GreeneryNeighborhoodResult(
                count.DistrictName,
                count.NeighborhoodName,
                count.GreenPixels,
                count.ValidPixels,
                value.RawValue,
                value.NormalizedValue));
        }

        var failures = new List<string>();
        new CoverageScope
        {
            NeighborhoodCount = counts.Count,
            DistrictCount = districtCount,
            ExpectedNeighborhoods = expectedNeighborhoods,
            ExpectedDistricts = expectedDistricts,
        }.AppendFailures(failures);
        new MetricCoverageCheck
        {
            MetricKey = series.Definition.Key,
            ExpectedValues = counts.Count,
            Values = series.Values,
            MaximumRaw = 100,
        }.AppendFailures(failures);

        var report = new GreeneryCheckReport(
            request.Manifest.Id,
            dataSourceId,
            request.Manifest.Name,
            request.Manifest.Url,
            request.Manifest.License,
            request.Manifest.AttributionText,
            request.Manifest.SourceVersion ?? string.Empty,
            checksum,
            raster.GdalVersion,
            loaded.PostgisVersion,
            raster.CroppedPath,
            raster.Width,
            raster.Height,
            raster.PixelSizeX,
            raster.PixelSizeY,
            loaded.TileCount,
            referenceYear,
            request.Manifest.ReferencePeriod,
            observedAt,
            computedAt,
            counts.Count,
            districtCount,
            expectedNeighborhoods,
            expectedDistricts,
            neighborhoods,
            failures);

        await GreeneryCheckReportWriter.WriteAsync(
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
            $"ETL: rapport WorldCover : " +
            $"{Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "greenery-check.md")}");

        return new EtlOutcome(
            success,
            checksum,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["neighborhoods"] = counts.Count,
                ["districts"] = districtCount,
                ["raster_tiles"] = loaded.TileCount,
                ["metrics"] = 1,
                ["metric_values"] = series.Values.Count,
                ["zero_neighborhoods"] = neighborhoods.Count(item => item.Share == 0),
                ["metric_id"] = (int)metricWrite.MetricIds[series.Definition.Key],
            },
            success ? null : string.Join(" ; ", failures));
    }

    private static int ParseReferenceYear(SourceManifest manifest)
    {
        if (manifest.ReferencePeriod.Length < 4
            || !int.TryParse(
                manifest.ReferencePeriod[..4],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var year))
        {
            throw new EtlFailureException(
                $"reference_period illisible dans le manifeste : " +
                $"« {manifest.ReferencePeriod} ».");
        }

        return year;
    }
}
