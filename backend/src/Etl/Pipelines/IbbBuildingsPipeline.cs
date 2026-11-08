using NeighbourhoodExplorer.Etl.Data;
using NeighbourhoodExplorer.Etl.Ibb;
using NeighbourhoodExplorer.Etl.Infrastructure;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed class IbbBuildingsPipeline(
    NpgsqlDataSource dataSource,
    IEtlConsole console,
    TimeProvider timeProvider,
    IbbNeighborhoodMatcher matcher) : IEtlPipeline
{
    public PipelineId Id => PipelineId.IbbBuildings2017;

    public async Task<EtlOutcome> RunAsync(
        EtlPipelineRequest request,
        CancellationToken cancellationToken)
    {
        var rawFile = SourceFileLocator.FindSingle(
            request.DataDirectory,
            request.Manifest.Id,
            "*.json");
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

        IbbDatastoreDocument document;
        await using (var stream = File.OpenRead(rawFile))
        {
            document = IbbDatastoreDocument.Parse(stream);
        }

        var prepared = IbbBuildingsPreparer.Prepare(document, request.Manifest.ExpectedCounts);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var osmNeighborhoods = await LoadOsmNeighborhoodsAsync(
            connection,
            transaction,
            cancellationToken);
        var match = matcher.Match(prepared.Rows, osmNeighborhoods);
        var computedAt = timeProvider.GetUtcNow();

        if (prepared.Failures.Count == 0)
        {
            await IbbStagingLoader.ReplaceAsync(
                connection,
                transaction,
                request.Manifest.Id,
                match.Rows,
                computedAt,
                cancellationToken);
        }

        var matchedByName = match.Rows.Count(row => row.MatchMethod == "name");
        var matchedByAlias = match.Rows.Count(row => row.MatchMethod == "alias");
        var matchedCount = matchedByName + matchedByAlias;
        var report = new IbbMatchReport(
            request.Manifest.Id,
            computedAt,
            rawFile,
            checksum,
            document.Records.Count,
            prepared.Counts,
            prepared.Failures,
            prepared.RepairedNames,
            matchedByName,
            matchedByAlias,
            match.AliasMatches,
            osmNeighborhoods.Count,
            match.Unmatched,
            osmNeighborhoods.Count == 0
                ? "admin_area ne contient aucun mahalle : importer OSM (pipeline osm-admin-boundaries) " +
                  "avant l'appariement."
                : null);

        await IbbMatchReportWriter.WriteAsync(request.DataDirectory, report, cancellationToken);

        var success = prepared.Failures.Count == 0;
        if (success)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        console.WriteLine(
            $"ETL: rapport d'appariement : " +
            $"{Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "match-report.md")}");

        return new EtlOutcome(
            success,
            checksum,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["rows"] = prepared.Rows.Count,
                ["matched"] = matchedCount,
                ["unmatched"] = match.Unmatched.Count,
            },
            success ? null : string.Join(" ; ", prepared.Failures));
    }

    private static async Task<IReadOnlyList<OsmNeighborhood>> LoadOsmNeighborhoodsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT n.id, d.name, n.name
            FROM admin_area n
            JOIN admin_area d ON d.id = n.parent_id
            WHERE n.level = 'neighborhood' AND d.level = 'district'
            ORDER BY n.id
            """;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        var neighborhoods = new List<OsmNeighborhood>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var district = reader.GetString(1);
            var neighborhood = reader.GetString(2);
            neighborhoods.Add(new OsmNeighborhood(
                reader.GetInt64(0),
                IbbNameNormalizer.NormalizeDistrict(district),
                IbbNameNormalizer.NormalizeNeighborhood(neighborhood),
                neighborhood));
        }

        return neighborhoods;
    }
}
