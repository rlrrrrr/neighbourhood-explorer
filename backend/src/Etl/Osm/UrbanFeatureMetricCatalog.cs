using NeighbourhoodExplorer.Etl.Database;

namespace NeighbourhoodExplorer.Etl.Osm;

public static class UrbanFeatureMetricCatalog
{
    public static IReadOnlyList<MetricDefinition> All { get; } =
    [
        new(
            "cafes_density",
            "Lifestyle",
            "cafés/km²",
            MetricPolarity.HigherIsBetter,
            1,
            MetricQualityFlag.Ok,
            "Cafés (amenity=cafe) d'OpenStreetMap, dédoublonnés par nom normalisé et " +
            "proximité (environ 55 m), comptés dans le polygone du mahalle puis divisés " +
            "par sa surface en km².",
            new MetricTranslation(
                "fr",
                "Densité de cafés",
                "Cafés recensés dans OpenStreetMap par km². La complétude d'OSM varie " +
                "selon les quartiers : c'est un indicateur, pas un recensement.",
                "les cafés"),
            new MetricTranslation(
                "en",
                "Café density",
                "Cafés mapped in OpenStreetMap per km². OSM completeness varies between " +
                "neighbourhoods: this is an indicator, not a census.",
                "cafés")),
        new(
            "restaurant_density",
            "Lifestyle",
            "restaurants/km²",
            MetricPolarity.HigherIsBetter,
            1,
            MetricQualityFlag.Ok,
            "Restaurants (amenity=restaurant) d'OpenStreetMap, dédoublonnés par nom " +
            "normalisé et proximité (environ 55 m), comptés dans le polygone du mahalle " +
            "puis divisés par sa surface en km².",
            new MetricTranslation(
                "fr",
                "Densité de restaurants",
                "Restaurants recensés dans OpenStreetMap par km². La complétude d'OSM " +
                "varie selon les quartiers : c'est un indicateur, pas un recensement.",
                "les restaurants"),
            new MetricTranslation(
                "en",
                "Restaurant density",
                "Restaurants mapped in OpenStreetMap per km². OSM completeness varies " +
                "between neighbourhoods: this is an indicator, not a census.",
                "restaurants")),
        new(
            "nightlife",
            "Lifestyle",
            "lieux/km²",
            MetricPolarity.HigherIsBetter,
            1,
            MetricQualityFlag.Ok,
            "Bars, pubs, discothèques et brasseries en plein air " +
            "(amenity=bar|pub|nightclub|biergarten) d'OpenStreetMap, dédoublonnés puis " +
            "comptés dans le polygone du mahalle et divisés par sa surface en km².",
            new MetricTranslation(
                "fr",
                "Vie nocturne",
                "Bars, pubs, discothèques et brasseries en plein air par km². OSM " +
                "sous-estime probablement la vie nocturne : indicateur, pas un recensement.",
                "la vie nocturne"),
            new MetricTranslation(
                "en",
                "Nightlife",
                "Bars, pubs, nightclubs and beer gardens per km². OSM probably " +
                "underestimates nightlife: an indicator, not a census.",
                "nightlife")),
        new(
            "culture",
            "Lifestyle",
            "lieux/km²",
            MetricPolarity.HigherIsBetter,
            1,
            MetricQualityFlag.Ok,
            "Musées et galeries (tourism=museum|gallery), théâtres, cinémas et centres " +
            "d'arts (amenity=theatre|cinema|arts_centre) d'OpenStreetMap, dédoublonnés " +
            "puis comptés dans le polygone du mahalle et divisés par sa surface en km².",
            new MetricTranslation(
                "fr",
                "Offre culturelle",
                "Musées, galeries, théâtres, cinémas et centres d'arts par km². " +
                "Indicateur issu des données OpenStreetMap disponibles.",
                "l'offre culturelle (musées, théâtres, cinémas)"),
            new MetricTranslation(
                "en",
                "Cultural offerings",
                "Museums, galleries, theatres, cinemas and arts centres per km². " +
                "An indicator based on available OpenStreetMap data.",
                "cultural offerings (museums, theaters, cinemas)")),
        new(
            "education_accessibility",
            "Education",
            "établissements à 800 m",
            MetricPolarity.HigherIsBetter,
            0,
            MetricQualityFlag.Ok,
            "Établissements d'enseignement (amenity=school|kindergarten|college|" +
            "university|library) d'OpenStreetMap, dédoublonnés et comptés dans un rayon " +
            "de 800 m autour du polygone du mahalle.",
            new MetricTranslation(
                "fr",
                "Accessibilité éducative",
                "Écoles, crèches, collèges, universités et bibliothèques distincts situés " +
                "à moins de 800 m du quartier. Indicateur de proximité, sans capacité ni " +
                "qualité des établissements.",
                "la proximité des écoles et des universités"),
            new MetricTranslation(
                "en",
                "Education accessibility",
                "Distinct schools, kindergartens, colleges, universities and libraries " +
                "within 800 m of the neighbourhood. A proximity indicator, without " +
                "capacity or quality of the facilities.",
                "being close to schools and universities")),
        new(
            "healthcare_accessibility",
            "Health",
            "établissements à 800 m",
            MetricPolarity.HigherIsBetter,
            0,
            MetricQualityFlag.Ok,
            "Établissements de santé (amenity=hospital|clinic|doctors|pharmacy) " +
            "d'OpenStreetMap, dédoublonnés et comptés dans un rayon de 800 m autour du " +
            "polygone du mahalle.",
            new MetricTranslation(
                "fr",
                "Accessibilité des soins",
                "Hôpitaux, cliniques, cabinets médicaux et pharmacies distincts situés à " +
                "moins de 800 m du quartier. Indicateur de proximité, sans capacité ni " +
                "horaires.",
                "la proximité des hôpitaux et des pharmacies"),
            new MetricTranslation(
                "en",
                "Healthcare accessibility",
                "Distinct hospitals, clinics, doctors and pharmacies within 800 m of the " +
                "neighbourhood. A proximity indicator, without capacity or opening hours.",
                "being close to hospitals and pharmacies")),
        new(
            "walkability",
            "Mobility",
            "indice 0–1",
            MetricPolarity.HigherIsBetter,
            2,
            MetricQualityFlag.Estimated,
            "Indice composite : 0,5 × percentile de la densité de carrefours (points dont " +
            "le degré routier est d'au moins trois) + 0,5 × percentile de la " +
            "densité de POI quotidiens, calculés sur les 964 mahalles. La valeur brute " +
            "est l'indice, la valeur normalisée est son percentile.",
            new MetricTranslation(
                "fr",
                "Marchabilité",
                "Indice relatif de possibilité de tout faire à pied, combinant la densité " +
                "de carrefours et celle des commerces et services de proximité. Estimation " +
                "issue d'OpenStreetMap, pas un score officiel de marche.",
                "la possibilité de tout faire à pied"),
            new MetricTranslation(
                "en",
                "Walkability",
                "Relative index of being able to do everything on foot, combining " +
                "intersection density and everyday amenities density. An estimate from " +
                "OpenStreetMap, not an official walk score.",
                "being able to do everything on foot")),
        new(
            "land_use_diversity",
            "Urban",
            "indice 0–1",
            MetricPolarity.HigherIsBetter,
            2,
            MetricQualityFlag.Estimated,
            "Entropie de Shannon normalisée par ln(6) sur les parts de six catégories " +
            "d'usage OpenStreetMap (résidentiel, commerces, restauration, culture, " +
            "bureaux, vert) intersectant le mahalle.",
            new MetricTranslation(
                "fr",
                "Diversité d'usages",
                "Mélange d'habitations, de commerces, de restauration, de culture, de " +
                "bureaux et d'espaces verts, mesuré par l'entropie de Shannon normalisée. " +
                "Dépend de la précision des balises de bâtiments dans OpenStreetMap.",
                "un mélange d'habitations, de commerces et de services"),
            new MetricTranslation(
                "en",
                "Land-use diversity",
                "Mix of housing, shops, food, culture, offices and green spaces, measured " +
                "by normalised Shannon entropy. Depends on building-tag precision in " +
                "OpenStreetMap.",
                "a mix of housing, shops and services")),
    ];

    public static string SourceMethodology =>
        "Comptages OpenStreetMap par mahalle : POI dédoublonnés par catégorie, nom " +
        "normalisé et proximité (environ 55 m), densités rapportées à la surface du " +
        "polygone administratif ; accessibilité éducative et sanitaire comptée dans un " +
        "rayon de 800 m. La marchabilité combine les percentiles de densité de carrefours " +
        "et de POI quotidiens ; la diversité d'usages est l'entropie de Shannon " +
        "normalisée de six catégories d'usage. Les listes de balises sont versionnées " +
        "dans backend/src/Etl/Osm/UrbanFeatureTagCatalog.cs.";
}
