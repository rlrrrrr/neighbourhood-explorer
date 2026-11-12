namespace NeighbourhoodExplorer.Etl.Osm;

public static class UrbanFeatureTagCatalog
{
    public static IReadOnlyList<string> CafeAmenities { get; } = ["cafe"];

    public static IReadOnlyList<string> RestaurantAmenities { get; } = ["restaurant"];

    public static IReadOnlyList<string> NightlifeAmenities { get; } =
        ["bar", "pub", "nightclub", "biergarten"];

    public static IReadOnlyList<string> CultureTourism { get; } = ["museum", "gallery"];

    public static IReadOnlyList<string> CultureAmenities { get; } =
        ["theatre", "cinema", "arts_centre"];

    public static IReadOnlyList<string> EducationAmenities { get; } =
        ["school", "kindergarten", "college", "university", "library"];

    public static IReadOnlyList<string> HealthcareAmenities { get; } =
        ["hospital", "clinic", "doctors", "pharmacy"];

    public static IReadOnlyList<string> DailyAmenities { get; } =
    [
        "cafe",
        "restaurant",
        "fast_food",
        "bakery",
        "pharmacy",
        "school",
        "kindergarten",
        "college",
        "university",
        "library",
        "doctors",
        "clinic",
        "hospital",
        "post_office",
        "bank",
        "marketplace",
    ];

    public static IReadOnlyList<string> LandUseCategories { get; } =
        ["residential", "commercial", "food", "culture", "office", "green"];

    public static IReadOnlyList<string> FoodLandUseAmenities { get; } =
        ["cafe", "restaurant", "fast_food", "bakery", "bar", "pub", "biergarten", "food_court"];

    public static IReadOnlyList<string> CommercialLandUses { get; } = ["commercial", "retail"];

    public static IReadOnlyList<string> CommercialBuildings { get; } =
        ["commercial", "retail", "hotel"];

    public static IReadOnlyList<string> ResidentialBuildings { get; } =
        ["apartments", "house", "residential", "dormitory"];

    public static IReadOnlyList<string> GreenLandUses { get; } =
        ["forest", "grass", "meadow", "recreation_ground", "village_green"];

    public static IReadOnlyList<string> GreenNaturals { get; } =
        ["wood", "scrub", "grassland", "heath"];

    public static IReadOnlyList<string> GreenLeisures { get; } =
        ["park", "garden", "playground", "pitch", "recreation_ground"];

    public static string SqlList(IReadOnlyList<string> values) =>
        string.Join(
            ", ",
            values.Select(value => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'"));
}
