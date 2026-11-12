using NeighbourhoodExplorer.Etl.Database;

namespace NeighbourhoodExplorer.Etl.Greenery;

public static class GreeneryMetricCatalog
{
    public static MetricDefinition Greenery { get; } = new(
        "greenery",
        "Environment",
        "% de la surface",
        MetricPolarity.HigherIsBetter,
        1,
        MetricQualityFlag.Estimated,
        "Part de la surface du mahalle couverte par arbres, arbustes et herbacées " +
        "(classes 10, 20 et 30 d'ESA WorldCover 2021 v200, 10 m), au pixel dont le " +
        "centre tombe dans le polygone administratif. Estimation satellite, pas un " +
        "inventaire des parcs publics.",
        new MetricTranslation(
            "fr",
            "Part d'espaces verts",
            "Part de la surface du quartier couverte par arbres, arbustes et herbacées " +
            "selon ESA WorldCover 2021. Estimation satellite à 10 m ; elle ne distingue " +
            "pas les jardins privés des parcs publics.",
            "les espaces verts"),
        new MetricTranslation(
            "en",
            "Green space share",
            "Share of the neighbourhood covered by trees, shrubs and grass according to " +
            "ESA WorldCover 2021. A 10 m satellite estimate; it does not distinguish " +
            "private gardens from public parks.",
            "green spaces"));

    public static string SourceMethodology =>
        "Part de la surface du mahalle couverte par les classes 10 (arbres), 20 " +
        "(arbustes) et 30 (herbacées) d'ESA WorldCover 2021 v200, tuile N39E027, " +
        "comptée au pixel de 10 m dont le centre tombe dans le polygone administratif. " +
        "Les pixels sans donnée sont exclus du dénominateur.";
}
