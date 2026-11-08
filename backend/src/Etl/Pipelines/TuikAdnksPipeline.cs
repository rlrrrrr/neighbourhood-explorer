using System.Globalization;
using System.Xml.Linq;
using NeighbourhoodExplorer.Etl.Infrastructure;
using NeighbourhoodExplorer.Etl.Manifests;
using NeighbourhoodExplorer.Etl.Tuik;
using Npgsql;

namespace NeighbourhoodExplorer.Etl.Pipelines;

public sealed class TuikAdnksPipeline(
    NpgsqlDataSource dataSource,
    IEtlConsole console,
    TimeProvider timeProvider,
    HttpClient httpClient,
    string? apiKey) : IEtlPipeline
{
    private const string CodelistUrl = "https://nsiws.tuik.gov.tr/rest/codelist/TR/CL_IBBS/1.2";

    public PipelineId Id => PipelineId.TuikAdnks;

    public async Task<EtlOutcome> RunAsync(
        EtlPipelineRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
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
        var expectedDistricts = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.Districts,
            request.Manifest.Id);
        var expectedProvincePopulation = request.Manifest.ExpectedCounts.Require(
            ExpectedCountField.ProvincePopulation,
            request.Manifest.Id);

        var tokenProvider = new TuikTokenProvider(
            httpClient,
            apiKey,
            new TuikTokenCache(),
            timeProvider);
        var client = new TuikSdmxClient(httpClient, tokenProvider);

        var codelistPath = Path.Combine(rawDirectory, "CL_IBBS-1.2.xml");
        var codelistXml = await client.GetAsync(CodelistUrl, cancellationToken);
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
                    $"population {provinceCode} {provincePopulation} ≠ attendu " +
                    $"{expectedProvincePopulation}.");
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
            && !string.Equals(
                checksum,
                request.Manifest.ChecksumSha256,
                StringComparison.OrdinalIgnoreCase))
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

        var retrievedAt = timeProvider.GetUtcNow();
        if (failures.Count == 0)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
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
                $"L'empreinte canonique porte sur les lignes « code=population » triées " +
                $"(ilçe + {provinceCode}), pas sur le XML brut dont l'en-tête SDMX varie à chaque requête."),
            cancellationToken);

        console.WriteLine(
            $"ETL: TÜİK ADNKS {referenceYear} : " +
            $"{totals.Count(total => total.Level == "district")} ilçe, " +
            $"{totals.Count(total => total.Level == "province")} province, " +
            $"{failures.Count} échec(s).");

        return failures.Count == 0
            ? new EtlOutcome(
                true,
                checksum,
                new Dictionary<string, int>(StringComparer.Ordinal)
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
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["province"] = totals.Count(total => total.Level == "province"),
                    ["district"] = totals.Count(total => total.Level == "district"),
                },
                string.Join(" ", failures));
    }

    private static string RequireManifestText(string? value, string key, string manifestId) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new EtlFailureException(
                $"champ « {key} » manquant dans le manifeste « {manifestId} ».")
            : value;
}
