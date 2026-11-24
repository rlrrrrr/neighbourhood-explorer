import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import type { components } from "../../generated/api";
import type { City } from "../metrics/metric-api-schema";
import type { ChoroplethFeatureCollection } from "./choropleth";
import { MapViewContent } from "./map-view-content";
import type { MapData } from "./map-data-client";
import type { MapDataState } from "./use-map-data";
import type { NeighborhoodMetricsState } from "./use-neighborhood-metrics";

type MetricDefinition = components["schemas"]["MetricDefinition"];

const metric: MetricDefinition = {
  key: "population_density",
  category: "People",
  unit: "people/km²",
  nativeLevel: "neighborhood",
  polarity: "neutral",
  normalization: { method: "min_max", version: "1" },
  displayPrecision: 0,
  translations: [
    {
      locale: "fr",
      name: "Densité de population",
      description: "Population par kilomètre carré.",
      subject: "Densité",
    },
  ],
  source: {
    id: "worldpop",
    name: "WorldPop",
    organization: "WorldPop",
    url: "https://www.worldpop.org/",
    license: "CC BY 4.0",
    attribution: "WorldPop 2025",
    methodology: "Estimation",
    retrievedAt: "2026-01-01",
    version: "2025",
  },
  observedAt: "2025-12-31",
};

const greenery: MetricDefinition = {
  ...metric,
  key: "greenery",
  category: "Environment",
  unit: "% de la surface",
  displayPrecision: 1,
  translations: [
    {
      locale: "fr",
      name: "Verdure",
      description: "Part de la surface verte.",
      subject: "la verdure",
    },
  ],
};

const collection: ChoroplethFeatureCollection = {
  type: "FeatureCollection",
  features: [
    {
      type: "Feature",
      geometry: {
        type: "Polygon",
        coordinates: [
          [
            [29, 41],
            [29, 41.01],
            [29.01, 41.01],
            [29, 41],
          ],
        ],
      },
      properties: {
        id: 1,
        name: "Moda",
        districtName: "Kadıköy",
        raw: 42,
        normalized: 0.4,
        qualityFlag: "estimated",
      },
    },
    {
      type: "Feature",
      geometry: {
        type: "Polygon",
        coordinates: [
          [
            [28.98, 41.03],
            [28.98, 41.04],
            [28.99, 41.04],
            [28.98, 41.03],
          ],
        ],
      },
      properties: {
        id: 2,
        name: "Cihangir",
        districtName: "Beyoğlu",
        raw: null,
        normalized: null,
        qualityFlag: null,
      },
    },
  ],
};

const data: MapData = { metric, metrics: [metric, greenery], collection };

const neighborhoodMetrics: NeighborhoodMetricsState = {
  status: "ready",
  data: {
    neighborhoodId: 1,
    metrics: [
      {
        key: "population_density",
        raw: 42,
        normalized: 0.4,
        qualityFlag: "estimated",
        observedAt: "2025-12-31",
        referencePeriod: "2025-12-31",
        source: {
          id: "worldpop",
          name: "WorldPop",
          organization: "WorldPop",
          license: "CC BY 4.0",
          attribution: "WorldPop 2025",
        },
      },
      {
        key: "greenery",
        raw: 17.3,
        normalized: 0.6,
        qualityFlag: "ok",
        observedAt: "2021-12-31",
        referencePeriod: "2021-12-31",
        source: {
          id: "esa-worldcover",
          name: "ESA WorldCover 2021",
          organization: "ESA WorldCover consortium",
          license: "CC BY 4.0",
          attribution: "© ESA WorldCover project 2021",
        },
      },
    ],
  },
};

function renderContent({
  state,
  selectedNeighborhoodId = null,
  metrics = neighborhoodMetrics,
  cityCenter = null,
}: {
  state: MapDataState;
  selectedNeighborhoodId?: number | null;
  metrics?: NeighborhoodMetricsState;
  cityCenter?: City["center"] | null;
}) {
  return renderToStaticMarkup(
    <MapViewContent
      cityName="Ville de test"
      cityCenter={cityCenter}
      metricKey="population_density"
      criteria={{ weights: [] }}
      onRemoveCriterion={() => {}}
      selectedNeighborhoodId={selectedNeighborhoodId}
      onSelectNeighborhood={() => {}}
      onMetricChange={() => {}}
      state={state}
      onReload={() => {}}
      neighborhoodMetrics={metrics}
      onReloadNeighborhoodMetrics={() => {}}
      colors={null}
      documentTheme="light"
    />,
  );
}

describe("map view content", () => {
  it("mounts the map only once the city center from /cities is known", () => {
    expect(renderContent({ state: { status: "loading" } })).not.toContain("neighborhood-map");

    expect(
      renderContent({
        state: { status: "loading" },
        cityCenter: { longitude: 28.98, latitude: 41.01 },
      }),
    ).toContain("neighborhood-map");
  });

  it("lists every loaded neighborhood and shows all metrics of the selected one", () => {
    const markup = renderContent({
      state: { status: "ready", data },
      selectedNeighborhoodId: 1,
    });

    expect(markup).toContain('<option value="1" selected="">Moda · Kadıköy</option>');
    expect(markup).toContain('<option value="2">Cihangir · Beyoğlu</option>');
    expect(markup).toContain("Densité de population");
    expect(markup).toContain("42");
    expect(markup).toContain("Verdure");
    expect(markup).toContain("17.3");
    expect(markup).toContain("Métriques affichées");
    expect(markup).toContain("2 / 2");
    expect(markup).toContain("Filtrer les métriques");
    expect(markup).toContain("width:60%");
    expect(markup).toContain("Estimation");
  });

  it("labels a neighborhood without a value instead of showing zero", () => {
    const markup = renderContent({
      state: { status: "ready", data },
      selectedNeighborhoodId: 2,
      metrics: {
        status: "ready",
        data: {
          neighborhoodId: 2,
          metrics: [
            {
              key: "population_density",
              raw: 0,
              normalized: 0,
              qualityFlag: "ok",
              observedAt: "2025-12-31",
              referencePeriod: "2025-12-31",
              source: {
                id: "worldpop",
                name: "WorldPop",
                organization: "WorldPop",
                license: "CC BY 4.0",
                attribution: "WorldPop 2025",
              },
            },
          ],
        },
      },
    });

    expect(markup).toContain("Pas de donnée");
    expect(markup).not.toContain("0 habitants/km²");
  });

  it("selects the exact neighborhood when names repeat across districts", () => {
    const duplicateData: MapData = {
      metric,
      metrics: [metric],
      collection: {
        type: "FeatureCollection",
        features: [
          collection.features[0],
          {
            ...collection.features[0],
            properties: {
              ...collection.features[0].properties,
              id: 3,
              districtName: "Üsküdar",
            },
          },
        ],
      },
    };

    const markup = renderContent({
      state: { status: "ready", data: duplicateData },
      selectedNeighborhoodId: 3,
    });

    expect(markup).toContain('<option value="3" selected="">Moda · Üsküdar</option>');
    expect(markup).toContain("Üsküdar");
  });

  it("shows the data source, license and observation date of the mapped metric", () => {
    const markup = renderContent({ state: { status: "ready", data } });

    expect(markup).toContain("WorldPop 2025");
    expect(markup).toContain("CC BY 4.0");
    expect(markup).toContain("2025-12-31");
  });

  it("offers a recenter action in the neighborhood picker", () => {
    const markup = renderContent({ state: { status: "ready", data } });

    expect(markup).toContain("Recentrer sur la ville");
    expect(markup).toContain("appearance-none");
  });

  it("keeps the five-step ramp and the missing-value pattern in the legend", () => {
    const markup = renderContent({ state: { status: "ready", data } });

    expect(markup.match(/bg-ramp-\d/g)).toHaveLength(5);
    expect(markup).toContain("no-data-pattern");
  });

  it("writes the raw value of each ramp stop with its unit in the legend", () => {
    const thresholdData: MapData = {
      ...data,
      collection: {
        type: "FeatureCollection",
        features: [0, 0.25, 0.5, 0.75, 1].map((normalized, index) => ({
          ...collection.features[0],
          properties: {
            ...collection.features[0].properties,
            id: index + 1,
            raw: (index + 1) * 100,
            normalized,
          },
        })),
      },
    };

    const markup = renderContent({ state: { status: "ready", data: thresholdData } });

    expect(markup).toContain("habitants/km²");

    for (const value of ["100", "200", "300", "400", "500"]) {
      expect(markup).toContain(`>${value}<`);
    }
  });

  it("explains the loading and error states", () => {
    expect(renderContent({ state: { status: "loading" } })).toContain("Chargement des quartiers…");

    const errorMarkup = renderContent({ state: { status: "error", code: "network_error" } });
    expect(errorMarkup).toContain("pas pu charger les données");
    expect(errorMarkup).toContain("Réessayer");
  });
});
