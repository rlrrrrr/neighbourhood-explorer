using System.Globalization;
using System.Xml.Linq;
using NeighbourhoodExplorer.Etl.Data;
using NeighbourhoodExplorer.Etl.Database;
using NeighbourhoodExplorer.Etl.Ibb;
using NeighbourhoodExplorer.Etl.Manifests;
using NeighbourhoodExplorer.Etl.Osm;
using NeighbourhoodExplorer.Etl.Tuik;
using NeighbourhoodExplorer.Etl.WorldPop;
using Npgsql;

namespace NeighbourhoodExplorer.Etl;

public sealed record EtlOutcome(
    bool Success,
    string? InputSha256,
    IReadOnlyDictionary<string, int> RowCounts,
    string? ErrorMessage);

public static class EtlApplication
{
    public static async Task<int> RunAsync(
        string[] arguments,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        EtlInvocation invocation;
        try
        {
            invocation = EtlInvocationParser.Parse(arguments);
        }
        catch (EtlUsageException exception)
        {
            error.WriteLine($"ETL: {exception.Message}");
            return 2;
        }

        var settings = EtlSettings.FromEnvironment();
        if (settings.ConnectionString is null)
        {
            error.WriteLine("ETL: DATABASE_CONNECTION_STRING manquant.");
            return 2;
        }

        var manifestPath = Path.Combine(settings.DataDirectory, "sources", $"{invocation.SourceId}.yaml");
        if (!File.Exists(manifestPath))
        {
            error.WriteLine($"ETL: manifeste introuvable : {manifestPath}");
            return 2;
        }

        SourceManifest manifest;
        try
        {
            manifest = SourceManifestReader.Read(manifestPath);
        }
        catch (ManifestException exception)
        {
            error.WriteLine($"ETL: manifeste invalide : {exception.Message}");
            return 2;
        }

        if (!string.Equals(manifest.Id, invocation.SourceId, StringComparison.Ordinal))
        {
            error.WriteLine(
                $"ETL: l'identifiant du manifeste « {manifest.Id} » ne correspond pas à la source demandée " +
                $"« {invocation.SourceId} ».");
            return 2;
        }

        var missingFields = SourceManifestReader.FindMissingFields(manifest);
        if (missingFields.Count > 0)
        {
            error.WriteLine(
                $"ETL: manifeste {manifestPath} incomplet : {string.Join(", ", missingFields)}. " +
                "Un humain doit avoir lu la licence et renseigné verified_by/verified_at ; ingestion refusée.");
            return 3;
        }

        await using var dataSource = CreateDataSource(settings.ConnectionString);
        var runRecorder = new EtlRunRecorder(dataSource, manifest.Id);
        long? runId = null;

        try
        {
            await EtlSchema.EnsureAsync(dataSource, cancellationToken);
            runId = await runRecorder.StartAsync(DateTimeOffset.UtcNow, cancellationToken);

            var outcome = manifest.Pipeline switch
            {
                "osm-admin-boundaries" => await RunOsmAdministrativeAreasAsync(
                    new OsmAdministrativeAreasRequest(
                        dataSource,
                        manifest,
                        settings.DataDirectory,
                        settings.ConnectionString,
                        invocation.ProvinceCode,
                        output),
                    cancellationToken),
                "ibb-buildings-2017" => await RunIbbBuildingsAsync(
                    new IbbBuildingsRequest(
                        dataSource,
                        manifest,
                        settings.DataDirectory,
                        output),
                    cancellationToken),
                "tuik-adnks" => await RunTuikAdnksAsync(
                    new TuikAdnksRequest(
                        dataSource,
                        manifest,
                        settings.DataDirectory,
                        settings.TuikApiKey,
                        output),
                    cancellationToken),
                "worldpop-istanbul-2025" => await RunWorldPopAsync(
                    new WorldPopRequest(
                        dataSource,
                        manifest,
                        settings.DataDirectory,
                        settings.ConnectionString,
                        output),
                    cancellationToken),
                _ => throw new EtlFailureException($"pipeline inconnu : {manifest.Pipeline}"),
            };

            await runRecorder.FinishAsync(
                runId.Value,
                new EtlRunResult(outcome.Success, outcome.InputSha256, outcome.RowCounts, outcome.ErrorMessage),
                cancellationToken);

            if (!outcome.Success)
            {
                error.WriteLine($"ETL: échec : {outcome.ErrorMessage}");
                return 1;
            }

            output.WriteLine("ETL: terminé.");
            return 0;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (runId is long failedRunId)
            {
                try
                {
                    await runRecorder.FinishAsync(
                        failedRunId,
                        new EtlRunResult(false, null, new Dictionary<string, int>(), exception.Message),
                        CancellationToken.None);
                }
                catch (Exception recorderException)
                {
                    error.WriteLine($"ETL: impossible d'enregistrer l'échec : {recorderException.Message}");
                }
            }

            error.WriteLine($"ETL: échec : {exception.Message}");
            return 1;
        }
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.ConnectionStringBuilder.CommandTimeout = Math.Max(
            builder.ConnectionStringBuilder.CommandTimeout,
            OfflineCommandTimeoutSeconds);
        return builder.Build();
    }

    private const int OfflineCommandTimeoutSeconds = 900;

    private static async Task<EtlOutcome> RunOsmAdministrativeAreasAsync(
        OsmAdministrativeAreasRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProvinceCode))
        {
            throw new EtlFailureException("option --province requise pour ce pipeline (ex. --province=TR-34).");
        }

        foreach (var level in new[] { "province", "district", "neighborhood" })
        {
            if (!request.Manifest.ExpectedCounts.ContainsKey(level))
            {
                throw new EtlFailureException(
                    $"le manifeste doit définir expected_counts.{level} (contrôle du nombre d'aires administratives).");
            }
        }

        var rawFile = SourceFileLocator.FindSingle(request.DataDirectory, request.Manifest.Id, "*.osm.pbf");
        var checksum = await Sha256Hasher.ComputeFileAsync(rawFile, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Manifest.ChecksumSha256)
            && !string.Equals(checksum, request.Manifest.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new EtlFailureException(
                $"SHA-256 du fichier brut ({checksum}) différent de celui du manifeste " +
                $"({request.Manifest.ChecksumSha256}).");
        }

        var osmImport = new OsmBoundaryImport(
            OsmDatabaseTarget.FromConnectionString(request.ConnectionString),
            request.Output);
        var osmVersion = await osmImport.RunAsync(
            new OsmImportRequest(request.DataSource, rawFile),
            cancellationToken);

        await using var connection = await request.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var build = await new AdminAreaBuilder(connection, transaction)
            .BuildAsync(new AdminAreaBuildRequest(request.Manifest.Id, request.ProvinceCode), cancellationToken);

        var report = await new AdminAreaQualityChecker(connection, transaction)
            .BuildAsync(
                new AdminAreaQualityRequest(
                    request.Manifest,
                    build,
                    rawFile,
                    checksum,
                    osmVersion),
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

        request.Output.WriteLine(
            $"ETL: rapport de qualité : {Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "quality-report.md")}");

        return new EtlOutcome(
            success,
            checksum,
            new Dictionary<string, int>
            {
                ["province"] = build.ProvinceCount,
                ["district"] = build.DistrictCount,
                ["neighborhood"] = build.NeighborhoodCount,
            },
            success ? null : string.Join(" ; ", report.Failures));
    }

    private sealed record OsmAdministrativeAreasRequest(
        NpgsqlDataSource DataSource,
        SourceManifest Manifest,
        string DataDirectory,
        string ConnectionString,
        string? ProvinceCode,
        TextWriter Output);

    private static async Task<EtlOutcome> RunIbbBuildingsAsync(
        IbbBuildingsRequest request,
        CancellationToken cancellationToken)
    {
        var rawFile = SourceFileLocator.FindSingle(request.DataDirectory, request.Manifest.Id, "*.json");
        var checksum = await Sha256Hasher.ComputeFileAsync(rawFile, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Manifest.ChecksumSha256)
            && !string.Equals(checksum, request.Manifest.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
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

        await using var connection = await request.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var osmNeighborhoods = await LoadOsmNeighborhoodsAsync(connection, transaction, cancellationToken);
        var match = IbbNeighborhoodMatcher.Match(prepared.Rows, osmNeighborhoods);

        if (prepared.Failures.Count == 0)
        {
            await IbbStagingLoader.ReplaceAsync(
                connection,
                transaction,
                request.Manifest.Id,
                match.Rows,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }

        var matchedByName = match.Rows.Count(row => row.MatchMethod == "name");
        var matchedByAlias = match.Rows.Count(row => row.MatchMethod == "alias");
        var matchedCount = matchedByName + matchedByAlias;
        var report = new IbbMatchReport(
            request.Manifest.Id,
            DateTimeOffset.UtcNow,
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

        request.Output.WriteLine(
            $"ETL: rapport d'appariement : " +
            $"{Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "match-report.md")}");

        return new EtlOutcome(
            success,
            checksum,
            new Dictionary<string, int>
            {
                ["rows"] = prepared.Rows.Count,
                ["matched"] = matchedCount,
                ["unmatched"] = match.Unmatched.Count,
            },
            success ? null : string.Join(" ; ", prepared.Failures));
    }

    private const string TuikCodelistUrl = "https://nsiws.tuik.gov.tr/rest/codelist/TR/CL_IBBS/1.2";

    private static int RequireExpectedCount(SourceManifest manifest, string key) =>
        manifest.ExpectedCounts.TryGetValue(key, out var value)
            ? value
            : throw new EtlFailureException(
                $"expected_counts.{key} manquant dans le manifeste « {manifest.Id} ».");

    private static string RequireManifestText(string? value, string key, string manifestId) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new EtlFailureException(
                $"champ « {key} » manquant dans le manifeste « {manifestId} ».")
            : value;

    private static async Task<EtlOutcome> RunTuikAdnksAsync(
        TuikAdnksRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new EtlFailureException(
                "TUIK_API_KEY est vide : renseigner la clé d'API TÜİK (variable d'environnement) " +
                "ou demander le XML brut à l'humain.");
        }

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

        var rawDirectory = Path.Combine(request.DataDirectory, "raw", request.Manifest.Id);
        Directory.CreateDirectory(rawDirectory);

        var provinceCode = RequireManifestText(
            request.Manifest.ProvinceCode,
            "province_code",
            request.Manifest.Id);
        var provinceName = RequireManifestText(
            request.Manifest.ProvinceName,
            "province_name",
            request.Manifest.Id);
        var expectedDistricts = RequireExpectedCount(request.Manifest, "districts");
        var expectedProvincePopulation = RequireExpectedCount(request.Manifest, "province_population");

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NeighbourhoodExplorerEtl/1.0");
        var tokenProvider = new TuikTokenProvider(httpClient, request.ApiKey, new TuikTokenCache());
        var client = new TuikSdmxClient(httpClient, tokenProvider);

        var codelistPath = Path.Combine(rawDirectory, "CL_IBBS-1.2.xml");
        var codelistXml = await client.GetAsync(TuikCodelistUrl, cancellationToken);
        await File.WriteAllTextAsync(codelistPath, codelistXml, cancellationToken);
        var districts = TuikCodelistParser.DistrictsOfProvince(
            XDocument.Parse(codelistXml),
            provinceCode);

        var dataPath = Path.Combine(
            rawDirectory,
            $"DF_ADNKS_T22-1.1-{referenceYear.ToString(CultureInfo.InvariantCulture)}.xml");
        var dataUrl =
            $"{request.Manifest.Url}?startPeriod={referenceYear.ToString(CultureInfo.InvariantCulture)}";
        var dataXml = await client.GetAsync(dataUrl, cancellationToken);
        await File.WriteAllTextAsync(dataPath, dataXml, cancellationToken);
        var values = TuikAdnksParser.Parse(XDocument.Parse(dataXml), referenceYear);

        var checks = new List<TuikCheck>();
        var failures = new List<string>();

        checks.Add(new TuikCheck(
            "ilçe (CL_IBBS 1.2)",
            expectedDistricts.ToString(CultureInfo.InvariantCulture),
            districts.Count.ToString(CultureInfo.InvariantCulture),
            districts.Count == expectedDistricts));
        if (districts.Count != expectedDistricts)
        {
            failures.Add($"codelist : {districts.Count} ilçe au lieu de {expectedDistricts}.");
        }

        var missingCodes = districts
            .Where(district => !values.ContainsKey(district.Code))
            .Select(district => district.Code)
            .ToList();
        checks.Add(new TuikCheck(
            "codes d'ilçe avec valeur",
            districts.Count.ToString(CultureInfo.InvariantCulture),
            (districts.Count - missingCodes.Count).ToString(CultureInfo.InvariantCulture),
            missingCodes.Count == 0));
        if (missingCodes.Count > 0)
        {
            failures.Add($"codes sans valeur ADNKS : {string.Join(", ", missingCodes)}.");
        }

        var hasProvince = values.ContainsKey(provinceCode);
        checks.Add(new TuikCheck(
            $"valeur {provinceCode}",
            "1",
            hasProvince ? "1" : "0",
            hasProvince));
        if (!hasProvince)
        {
            failures.Add($"valeur ADNKS de {provinceCode} absente.");
        }

        if (missingCodes.Count == 0 && hasProvince)
        {
            var districtSum = districts.Sum(district => (long)values[district.Code]);
            var provincePopulation = values[provinceCode];
            checks.Add(new TuikCheck(
                $"somme des ilçe = {provinceCode}",
                provincePopulation.ToString(CultureInfo.InvariantCulture),
                districtSum.ToString(CultureInfo.InvariantCulture),
                districtSum == provincePopulation));
            if (districtSum != provincePopulation)
            {
                failures.Add($"somme des ilçe {districtSum} ≠ {provinceCode} {provincePopulation}.");
            }

            checks.Add(new TuikCheck(
                $"population {provinceCode}",
                expectedProvincePopulation.ToString(CultureInfo.InvariantCulture),
                provincePopulation.ToString(CultureInfo.InvariantCulture),
                provincePopulation == expectedProvincePopulation));
            if (provincePopulation != expectedProvincePopulation)
            {
                failures.Add(
                    $"population {provinceCode} {provincePopulation} ≠ attendu {expectedProvincePopulation}.");
            }
        }

        var canonicalValues = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var district in districts)
        {
            if (values.TryGetValue(district.Code, out var population))
            {
                canonicalValues[district.Code] = population;
            }
        }

        if (values.TryGetValue(provinceCode, out var provinceValue))
        {
            canonicalValues[provinceCode] = provinceValue;
        }

        var checksum = TuikCanonicalChecksum.Compute(canonicalValues);
        if (!string.IsNullOrWhiteSpace(request.Manifest.ChecksumSha256)
            && !string.Equals(checksum, request.Manifest.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"empreinte canonique {checksum} différente du manifeste " +
                $"{request.Manifest.ChecksumSha256} (données révisées ?).");
        }

        var totals = new List<TuikTotal>();
        if (missingCodes.Count == 0 && hasProvince)
        {
            totals.Add(new TuikTotal(
                "province",
                provinceCode,
                provinceName,
                values[provinceCode]));
            totals.AddRange(districts.Select(district =>
                new TuikTotal("district", district.Code, district.Name, values[district.Code])));
        }

        var retrievedAt = DateTimeOffset.UtcNow;
        if (failures.Count == 0)
        {
            await using var connection = await request.DataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction =
                (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await TuikTotalsLoader.ReplaceAsync(
                connection,
                transaction,
                request.Manifest.Id,
                referenceYear,
                retrievedAt,
                totals,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await TuikCheckReportWriter.WriteAsync(
            request.DataDirectory,
            new TuikCheckReport(
                request.Manifest.Id,
                referenceYear,
                retrievedAt,
                checksum,
                codelistPath,
                dataPath,
                checks,
                failures,
                totals,
                $"L'empreinte canonique porte sur les lignes « code=population » triées (ilçe + {provinceCode}), " +
                "pas sur le XML brut dont l'en-tête SDMX varie à chaque requête."),
            cancellationToken);

        request.Output.WriteLine(
            $"ETL: TÜİK ADNKS {referenceYear} : " +
            $"{totals.Count(total => total.Level == "district")} ilçe, " +
            $"{totals.Count(total => total.Level == "province")} province, " +
            $"{failures.Count} échec(s).");

        return failures.Count == 0
            ? new EtlOutcome(
                true,
                checksum,
                new Dictionary<string, int>
                {
                    ["province"] = totals.Count(total => total.Level == "province"),
                    ["district"] = totals.Count(total => total.Level == "district"),
                    ["population"] = totals
                        .Where(total => total.Level == "district")
                        .Sum(total => total.Population),
                },
                null)
            : new EtlOutcome(
                false,
                checksum,
                new Dictionary<string, int>
                {
                    ["province"] = totals.Count(total => total.Level == "province"),
                    ["district"] = totals.Count(total => total.Level == "district"),
                },
                string.Join(" ", failures));
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

    private sealed record IbbBuildingsRequest(
        NpgsqlDataSource DataSource,
        SourceManifest Manifest,
        string DataDirectory,
        TextWriter Output);

    private sealed record TuikAdnksRequest(
        NpgsqlDataSource DataSource,
        SourceManifest Manifest,
        string DataDirectory,
        string? ApiKey,
        TextWriter Output);

    private static async Task<EtlOutcome> RunWorldPopAsync(
        WorldPopRequest request,
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

        var expectedNeighborhoods = RequireExpectedCount(request.Manifest, "neighborhoods");
        var expectedDistricts = RequireExpectedCount(request.Manifest, "districts");
        var expectedProvincePopulation = RequireExpectedCount(request.Manifest, "province_population");
        var gridYear = request.Manifest.GridYear ?? referenceYear;

        var rawFile = SourceFileLocator.FindSingle(request.DataDirectory, request.Manifest.Id, "*.tif");
        var checksum = await Sha256Hasher.ComputeFileAsync(rawFile, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Manifest.ChecksumSha256)
            && !string.Equals(checksum, request.Manifest.ChecksumSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new EtlFailureException(
                $"SHA-256 du fichier brut ({checksum}) différent de celui du manifeste " +
                $"({request.Manifest.ChecksumSha256}).");
        }

        var bounds = await LoadProvinceBoundsAsync(request.DataSource, cancellationToken);
        var projected = await WorldPopRasterProjector.ProjectAsync(
            new WorldPopProjectionRequest(
                request.Manifest.Id,
                rawFile,
                Path.Combine(request.DataDirectory, "processed", request.Manifest.Id),
                bounds),
            cancellationToken);

        request.Output.WriteLine(
            $"ETL: WorldPop grille {gridYear} projeté en EPSG:32635 " +
            $"({projected.Width} × {projected.Height} cases, {projected.ValidCellCount:N0} valides).");

        var target = OsmDatabaseTarget.FromConnectionString(request.ConnectionString);
        var raster = await WorldPopRasterLoader.LoadAsync(
            new WorldPopRasterLoadRequest(
                request.DataSource,
                target,
                projected.ProjectedPath,
                request.Output),
            cancellationToken);

        await using var connection = await request.DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var zonal = await WorldPopZonalSummer.ComputeAsync(
            new WorldPopZonalSumRequest(
                connection,
                transaction,
                request.Manifest.Id,
                gridYear),
            cancellationToken);

        request.Output.WriteLine(
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
                expectedNeighborhoods,
                expectedDistricts,
                expectedProvincePopulation,
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
            DateTimeOffset.UtcNow,
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

        request.Output.WriteLine(
            $"ETL: rapport WorldPop : " +
            $"{Path.Combine(request.DataDirectory, "processed", request.Manifest.Id, "worldpop-check.md")}");

        return new EtlOutcome(
            success,
            checksum,
            new Dictionary<string, int>
            {
                ["neighborhoods"] = recalibration.NeighborhoodCount,
                ["districts"] = recalibration.DistrictCount,
                ["population"] = (int)Math.Round(recalibration.TotalEstimated),
                ["flagged_coefficients"] = recalibration.FlaggedCount,
                ["zero_neighborhoods"] = zonal.ZeroCount,
            },
            success ? null : string.Join(" ; ", failures));
    }

    private static async Task<WorldPopBounds> LoadProvinceBoundsAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ST_XMin(bounds), ST_YMin(bounds), ST_XMax(bounds), ST_YMax(bounds)
            FROM (
                SELECT ST_Envelope(ST_Collect(ST_Transform(geometry, 32635))) AS bounds
                FROM admin_area
                WHERE level = 'province'
            ) AS province
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || await reader.IsDBNullAsync(0, cancellationToken))
        {
            throw new EtlFailureException(
                "admin_area ne contient aucune province : importer OSM avant WorldPop.");
        }

        return new WorldPopBounds(
            reader.GetDouble(0),
            reader.GetDouble(1),
            reader.GetDouble(2),
            reader.GetDouble(3));
    }

    private sealed record WorldPopRequest(
        NpgsqlDataSource DataSource,
        SourceManifest Manifest,
        string DataDirectory,
        string ConnectionString,
        TextWriter Output);
}
