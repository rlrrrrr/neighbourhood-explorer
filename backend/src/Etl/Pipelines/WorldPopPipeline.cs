using System.Globalization;
using NeighbourhoodExplorer.Etl.Data;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Manifests;
using NeighbourhoodExplorer.Etl.Osm;
using NeighbourhoodExplorer.Etl.WorldPop;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed class WorldPopPipeline(
    NpgsqlDataSource dataSource,
    OsmDatabaseTarget target,
    IExternalToolRunner toolRunner,
    IEtlConsole console,
    TimeProvider timeProvider) : IEtlPipeline
{
    public PipelineId Id => PipelineId.WorldPopIstanbul2025;

    public async Task<EtlOutcome> RunAsync(
        EtlPipelineRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Manifest.ReferencePeriod.Length < 4
            || !int.TryParse(
                request.Manifest.ReferencePeriod[..4],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var referenceYear))
        {
            throw new EtlFailureException(
                $"reference_period illisible dans le manifeste : « {request.Manifest.ReferencePeriod} ».");
        }

        var expectedNeighborhoods = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Neighborhoods,
            request.Manifest.Id);
        var expectedDistricts = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Districts,
            request.Manifest.Id);
        var expectedProvincePopulation = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.ProvincePopulation,
            request.Manifest.Id);
        var gridYear = request.Manifest.GridYear ?? referenceYear;

        var rawFile = SourceFileLocator.FindSingle(
            request.DataDirectory,
            request.Manifest.Id,
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

        var bounds = await WorldPopProvinceBoundsReader.ReadAsync(dataSource, cancellationToken);
        var projector = new WorldPopRasterProjector(toolRunner);
        var projected = await projector.ProjectAsync(
            new WorldPopProjectionRequest(
                request.Manifest.Id,
                rawFile,
                Path.Combine(request.DataDirectory, "processed", request.Manifest.Id),
                bounds),
            cancellationToken);

        console.WriteLine(
            $"ETL: WorldPop grille {gridYear} projeté en EPSG:32635 " +
            $"({projected.Width} × {projected.Height} cases, {projected.ValidCellCount:N0} valides).");

        var rasterLoader = new WorldPopRasterLoader(dataSource, toolRunner, console);
        var raster = await rasterLoader.LoadAsync(
            new WorldPopRasterLoadRequest(target, projected.ProjectedPath),
            cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var computedAt = timeProvider.GetUtcNow();
        var zonal = await WorldPopZonalSummer.ComputeAsync(
            new WorldPopZonalSumRequest(
                connection,
                transaction,
                request.Manifest.Id,
                gridYear,
                computedAt),
            cancellationToken);

        console.WriteLine(
            $"ETL: WorldPop grille {gridYear} : {zonal.NeighborhoodCount} mahalles, " +
            $"{zonal.ZeroCount} somme(s) nulle(s), total brut {zonal.TotalRaw:F3}.");

        var observedAt = new DateOnly(referenceYear, 12, 31);
        var referencePeriod = referenceYear.ToString(CultureInfo.InvariantCulture);
        var metadata = new WorldPopMetricMetadata(
            request.Manifest.Id,
            request.Manifest.Name,
            request.Manifest.Organization,
            request.Manifest.Url,
            request.Manifest.License,
            request.Manifest.AttributionText,
            $"Somme des cases WorldPop {gridYear} (100 m, contrainte aux bâtiments) par mahalle, " +
            "avec pondération par la fraction de case recouverte ; recalage par ilçe sur les " +
            $"totaux ADNKS {referenceYear}.",
            DateOnly.Parse(request.Manifest.RetrievedAt!, CultureInfo.InvariantCulture),
            request.Manifest.SourceVersion ?? string.Empty,
            "population_density",
            "People",
            "people/km²",
            "percentile",
            "percentile-v1",
            0,
            referencePeriod,
            observedAt,
            [
                new WorldPopMetricTranslation(
                    "fr",
                    "Densité de population",
                    $"Population estimée par km² (WorldPop {gridYear} recalée sur les totaux ADNKS " +
                    $"{referenceYear} par ilçe). Estimation, pas un recensement.",
                    "la densité de population"),
                new WorldPopMetricTranslation(
                    "en",
                    "Population density",
                    $"Estimated population per km² (WorldPop {gridYear} recalibrated to " +
                    $"{referenceYear} ADNKS district totals). An estimate, not a census.",
                    "population density"),
            ]);

        var recalibration = await WorldPopRecalibrator.RecalculateAsync(
            new WorldPopRecalibrationRequest(
                connection,
                transaction,
                request.Manifest.Id,
                gridYear,
                referenceYear,
                new WorldPopExpectedCounts(
                    expectedNeighborhoods,
                    expectedDistricts,
                    expectedProvincePopulation),
                computedAt,
                metadata),
            cancellationToken);

        var failures = recalibration.Failures.ToList();
        if (zonal.NeighborhoodCount != expectedNeighborhoods)
        {
            failures.Add(
                $"WorldPop : {zonal.NeighborhoodCount} mahalles au lieu de {expectedNeighborhoods}.");
        }

        var report = new WorldPopCheckReport(
            request.Manifest.Id,
            request.Manifest.Name,
            request.Manifest.Url,
            request.Manifest.License,
            request.Manifest.AttributionText,
            request.Manifest.SourceVersion ?? string.Empty,
            checksum,
            projected.GdalVersion,
            raster.PostgisVersion,
            gridYear,
            referenceYear,
            observedAt,
            referencePeriod,
            rawFile,
            projected.ProjectedPath,
            projected.Width,
            projected.Height,
            projected.Srid,
            projected.PixelSizeX,
            projected.PixelSizeY,
            projected.NoDataValue,
            projected.Minimum,
            projected.Maximum,
            projected.Mean,
            projected.ValidPercent,
            raster.ValidCellCount,
            raster.TileCount,
            raster.TotalPopulation,
            recalibration.NeighborhoodCount,
            recalibration.DistrictCount,
            recalibration.TotalRaw,
            recalibration.TotalAdnks,
            recalibration.TotalEstimated,
            recalibration.FlaggedCount,
            zonal.ZeroCount,
            recalibration.MinDensity,
            recalibration.MaxDensity,
            recalibration.MedianDensity,
            recalibration.MetricId,
            metadata.MetricKey,
            computedAt,
            recalibration.Coefficients,
            recalibration.Neighborhoods,
            zonal.Zeros,
            recalibration.UnmatchedDistricts,
            failures);

        await WorldPopCheckReportWriter.WriteAsync(
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
            $"ETL: rapport WorldPop : " +
            $"{Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "worldpop-check.md")}");

        return new EtlOutcome(
            success,
            checksum,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["neighborhoods"] = recalibration.NeighborhoodCount,
                ["districts"] = recalibration.DistrictCount,
                ["population"] = (int)Math.Round(recalibration.TotalEstimated),
                ["flagged_coefficients"] = recalibration.FlaggedCount,
                ["zero_neighborhoods"] = zonal.ZeroCount,
            },
            success ? null : string.Join(" ; ", failures));
    }
}
