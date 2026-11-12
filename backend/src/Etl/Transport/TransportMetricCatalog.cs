using NeighbourhoodExplorer.Etl.Database;

namespace NeighbourhoodExplorer.Etl.Transport;

public static class TransportMetricCatalog
{
    public static MetricDefinition TransportAccessibility { get; } = new(
        "transport_accessibility",
        "Mobility",
        "points pondérés/km²",
        MetricPolarity.HigherIsBetter,
        1,
        MetricQualityFlag.Ok,
        "Arrêts de bus İETT, stations de métro, tramway et funiculaire de Metro " +
        "İstanbul et pontons Şehir Hatları, dédoublonnés par nom normalisé et " +
        "proximité (60 m pour les bus, 150 m pour le rail et les ferries), comptés " +
        "dans le polygone du mahalle ou rattachés au mahalle le plus proche à moins " +
        "de 500 m pour les points en mer, puis pondérés (bus 1, rail 3, ferry 3) et " +
        "divisés par la surface du mahalle en km².",
        new MetricTranslation(
            "fr",
            "Accès aux transports en commun",
            "Densité d'arrêts et de stations de transport en commun, pondérée par mode " +
            "(bus 1, rail 3, ferry 3) et rapportée à la surface du quartier. Elle ne " +
            "mesure ni la fréquence ni la qualité de service.",
            "l'accès aux transports en commun"),
        new MetricTranslation(
            "en",
            "Access to public transport",
            "Density of public transport stops and stations, weighted by mode " +
            "(bus 1, rail 3, ferry 3) and divided by the neighbourhood area. It measures " +
            "neither service frequency nor service quality.",
            "access to public transport"));

    public const int BusWeight = 1;

    public const int RailWeight = 3;

    public const int FerryWeight = 3;

    public static string SourceMethodology =>
        "Densité pondérée d'arrêts et de stations İBB : arrêts de bus İETT (poids 1), " +
        "stations Metro İstanbul (poids 3) et pontons Şehir Hatları (poids 3), après " +
        "dédoublonnage par nom normalisé et proximité (60 m bus, 150 m rail et ferry) ; " +
        "les points hors polygone sont rattachés au mahalle le plus proche à moins de " +
        "500 m. Valeur brute = points pondérés / surface du mahalle en km², normalisée " +
        "par percentile sur les 964 mahalles.";
}
