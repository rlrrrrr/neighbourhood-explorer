using NeighbourhoodExplorer.Etl.WorldPop;

namespace NeighbourhoodExplorer.Etl.Tests;

public sealed class WorldPopCheckReportWriterTests
{
    [Fact]
    public async Task Writer_ProducesMarkdownJsonAndCsv()
    {
        var dataDirectory = Path.Combine(
            Path.GetTempPath(),
            $"worldpop-report-{Guid.NewGuid():N}");

        try
        {
            var report = new WorldPopCheckReport(
                SourceId: "worldpop-test",
                SourceName: "WorldPop test",
                SourceUrl: "https://example.org/worldpop",
                License: "CC BY 4.0",
                AttributionText: "WorldPop test, valeurs modifiées",
                SourceVersion: "test-v1",
                ChecksumSha256: new string('a', 64),
                GdalVersion: "GDAL test",
                PostgisVersion: "PostGIS test",
                GridYear: 2025,
                ReferenceYear: 2025,
                ObservedAt: new DateOnly(2025, 12, 31),
                ReferencePeriod: "2025",
                RawFilePath: "/raw.tif",
                ProjectedFilePath: "/projected.tif",
                RasterWidth: 10,
                RasterHeight: 10,
                RasterSrid: 32635,
                PixelSizeX: 100,
                PixelSizeY: -100,
                NoDataValue: -99999,
                RasterMinimum: 0,
                RasterMaximum: 10,
                RasterMean: 5,
                RasterValidPercent: 50,
                RasterValidCells: 50,
                RasterTileCount: 1,
                RasterTotalPopulation: 100,
                NeighborhoodCount: 1,
                DistrictCount: 1,
                TotalRaw: 100,
                TotalAdnks: 100,
                TotalEstimated: 100,
                FlaggedCoefficientCount: 0,
                ZeroNeighborhoodCount: 0,
                MinDensity: 1,
                MaxDensity: 2,
                MedianDensity: 1.5,
                MetricId: 42,
                MetricKey: "population_density",
                ComputedAt: DateTimeOffset.UtcNow,
                Coefficients:
                [
                    new WorldPopCoefficient(
                        1,
                        "Arnavutköy",
                        "2048",
                        "Arnavutköy",
                        100,
                        100,
                        1,
                        false,
                        1,
                        0),
                ],
                Neighborhoods:
                [
                    new WorldPopNeighborhoodResult(
                        42,
                        "Arnavutköy",
                        "Mahalle, A",
                        100,
                        1,
                        1,
                        100,
                        100,
                        0.5,
                        "estimated"),
                ],
                ZeroNeighborhoods: [],
                UnmatchedDistricts: [],
                Failures: ["échec test"]);

            await WorldPopCheckReportWriter.WriteAsync(
                dataDirectory,
                report,
                CancellationToken.None);

            var outputDirectory = Path.Combine(dataDirectory, "processed", "worldpop-test");
            var markdown = await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "worldpop-check.md"));
            var json = await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "worldpop-check.json"));
            var csv = await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "population-density-mahalle.csv"));

            Assert.Contains("**Résultat : 1 échec(s).**", markdown, StringComparison.Ordinal);
            Assert.Contains("échec test", markdown, StringComparison.Ordinal);
            Assert.Contains("worldpop-test", json, StringComparison.Ordinal);
            Assert.Contains("\"Mahalle, A\"", csv, StringComparison.Ordinal);
            Assert.Contains("estimated", csv, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, true);
            }
        }
    }
}
