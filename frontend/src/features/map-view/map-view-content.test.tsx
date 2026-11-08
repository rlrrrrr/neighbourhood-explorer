import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import type { components } from "../../generated/api";
import type { ChoroplethFeatureCollection } from "./choropleth";
import { MapViewContent } from "./map-view-content";
import type { MapData } from "./map-data-client";
import type { MapDataState } from "./use-map-data";

type MetricDefinition = components["schemas"]["MetricDefinition"];

const metric: MetricDefinition = {
  key: "population_density",
  category: "population",
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

const data: MapData = { metric, collection };

function renderContent({
  state,
  selectedNeighborhoodId = null,
}: {
  state: MapDataState;
  selectedNeighborhoodId?: number | null;
}) {
  return renderToStaticMarkup(
    <MapViewContent
      criteria={{ weights: [] }}
      onRemoveCriterion={() => {}}
      selectedNeighborhoodId={selectedNeighborhoodId}
      onSelectNeighborhood={() => {}}
      state={state}
      onReload={() => {}}
      colors={null}
      documentTheme="light"
    />,
  );
}

describe("map view content", () => {
  it("lists every loaded neighborhood and shows the selected value", () => {
    const markup = renderContent({
      state: { status: "ready", data },
      selectedNeighborhoodId: 1,
    });

    expect(markup).toContain('<option value="1" selected="">Moda · Kadıköy</option>');
    expect(markup).toContain('<option value="2">Cihangir · Beyoğlu</option>');
    expect(markup).toContain("42 habitants/km²");
    expect(markup).toContain("Estimation");
    expect(markup).toContain("Densité de population");
  });

  it("labels a neighborhood without a value instead of showing zero", () => {
    const markup = renderContent({
      state: { status: "ready", data },
      selectedNeighborhoodId: 2,
    });

    expect(markup).toContain("Pas de donnée");
    expect(markup).not.toContain("0 habitants/km²");
  });

  it("selects the exact neighborhood when names repeat across districts", () => {
    const duplicateData: MapData = {
      metric,
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
  it("shows the data source, license and observation date", () => {
    const markup = renderContent({ state: { status: "ready", data } });

    expect(markup).toContain("WorldPop 2025");
    expect(markup).toContain("CC BY 4.0");
    expect(markup).toContain("2025-12-31");
  });

  it("offers a recenter action in the neighborhood picker", () => {
    const markup = renderContent({ state: { status: "ready", data } });

    expect(markup).toContain("Recentrer sur Istanbul");
    expect(markup).toContain("appearance-none");
  });

  it("keeps the five-step ramp and the missing-value pattern in the legend", () => {
    const markup = renderContent({ state: { status: "ready", data } });

    expect(markup.match(/bg-ramp-\d/g)).toHaveLength(5);
    expect(markup).toContain("no-data-pattern");
  });

  it("explains the loading and error states", () => {
    expect(renderContent({ state: { status: "loading" } })).toContain("Chargement des quartiers…");

    const errorMarkup = renderContent({ state: { status: "error", code: "network_error" } });
    expect(errorMarkup).toContain("pas pu charger les données");
    expect(errorMarkup).toContain("Réessayer");
  });
});
