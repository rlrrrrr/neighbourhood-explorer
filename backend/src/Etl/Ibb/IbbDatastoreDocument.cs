using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeighbourhoodExplorer.Etl.Ibb;

public sealed record IbbDatastoreDocument(int Total, IReadOnlyList<IbbBuildingRecord> Records)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IbbDatastoreDocument Parse(Stream stream)
    {
        var envelope = JsonSerializer.Deserialize<IbbDatastoreEnvelope>(stream, JsonOptions)
            ?? throw new EtlFailureException("réponse datastore İBB vide.");

        if (!envelope.Success || envelope.Result is null)
        {
            throw new EtlFailureException("réponse datastore İBB en échec (success=false).");
        }

        return new IbbDatastoreDocument(envelope.Result.Total, envelope.Result.Records);
    }

    private sealed record IbbDatastoreEnvelope(
        bool Success,
        IbbDatastoreResult? Result);

    private sealed record IbbDatastoreResult(
        int Total,
        IReadOnlyList<IbbBuildingRecord> Records);
}

public sealed record IbbBuildingRecord(
    [property: JsonPropertyName("_id")] int Id,
    [property: JsonPropertyName("ilce_adi")] string DistrictName,
    [property: JsonPropertyName("mahalle_adi")] string NeighborhoodName,
    [property: JsonPropertyName("mahalle_uavt")] long MahalleUavt,
    [property: JsonPropertyName("1980_oncesi")] int BuildingsBefore1980,
    [property: JsonPropertyName("1980-2000_arasi")] int BuildingsBetween1980And2000,
    [property: JsonPropertyName("2000_sonrasi")] int BuildingsAfter2000,
    [property: JsonPropertyName("1-4 kat_arasi")] int BuildingsWith1To4Floors,
    [property: JsonPropertyName("5-9 kat_arasi")] int BuildingsWith5To9Floors,
    [property: JsonPropertyName("9-19 kat_arasi")] int BuildingsWith9To19Floors)
{
    public int BuildingsByAge => BuildingsBefore1980 + BuildingsBetween1980And2000 + BuildingsAfter2000;

    public int BuildingsByFloors => BuildingsWith1To4Floors + BuildingsWith5To9Floors + BuildingsWith9To19Floors;
}
