using NeighbourhoodExplorer.Etl.Data;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Manifests;
using NeighbourhoodExplorer.Etl.Osm;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed class OsmAdministrativeAreasPipeline(
    NpgsqlDataSource dataSource,
    OsmDatabaseTarget target,
    IExternalToolRunner toolRunner,
    IEtlConsole console,
    TimeProvider timeProvider) : IEtlPipeline
{
    public PipelineId Id => PipelineId.OsmAdminBoundaries;

    public async Task<EtlOutcome> RunAsync(
        EtlPipelineRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProvinceCode))
        {
            throw new EtlFailureException(
                "option --province requise pour ce pipeline (ex. --province=TR-34).");
        }

        request.Manifest.ExpectedCounts.Require(ExpectedCountField.Province, request.Manifest.Id);
        request.Manifest.ExpectedCounts.Require(ExpectedCountField.Districts, request.Manifest.Id);
        request.Manifest.ExpectedCounts.Require(ExpectedCountField.Neighborhoods, request.Manifest.Id);

        var rawFile = SourceFileLocator.FindSingle(
            request.DataDirectory,
            request.Manifest.Id,
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

        var osmImport = new OsmBoundaryImport(target, toolRunner, console);
        var osmVersion = await osmImport.RunAsync(
            new OsmImportRequest(dataSource, rawFile),
            cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var build = await new AdminAreaBuilder(connection, transaction)
            .BuildAsync(
                new AdminAreaBuildRequest(request.Manifest.Id, request.ProvinceCode),
                cancellationToken);

        var report = await new AdminAreaQualityChecker(connection, transaction)
            .BuildAsync(
                new AdminAreaQualityRequest(
                    request.Manifest,
                    build,
                    rawFile,
                    checksum,
                    osmVersion,
                    timeProvider.GetUtcNow()),
                cancellationToken);

        await QualityReportWriter.WriteAsync(request.DataDirectory, report, cancellationToken);

        var success = report.Failures.Count == 0;
        if (success)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        console.WriteLine(
            $"ETL: rapport de qualité : " +
            $"{Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "quality-report.md")}");

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["province"] = build.ProvinceCount,
            ["district"] = build.DistrictCount,
            ["neighborhood"] = build.NeighborhoodCount,
        };

        return new EtlOutcome(
            success,
            checksum,
            counts,
            success ? null : string.Join(" ; ", report.Failures));
    }
}
