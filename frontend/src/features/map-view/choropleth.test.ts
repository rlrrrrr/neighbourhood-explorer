import { describe, expect, it } from "vite-plus/test";
import type { components } from "../../generated/api";
import {
  buildChoroplethFeatureCollection,
  collectionBounds,
  selectFeaturesById,
  splitFeaturesByMetricValue,
  type ChoroplethFeatureCollection,
} from "./choropleth";

type NeighborhoodCollection = components["schemas"]["NeighborhoodFeatureCollection"];
type MetricValues = components["schemas"]["MetricValues"];

const MODA_POLYGON = [
  [
    [29, 41],
    [29, 41.01],
    [29.01, 41.01],
    [29, 41],
  ],
];

const neighborhoods: NeighborhoodCollection = {
  type: "FeatureCollection",
  features: [
    {
      type: "Feature",
      geometry: { type: "Polygon", coordinates: MODA_POLYGON },
      properties: { id: 1, name: "Moda", districtId: 10, districtName: "Kadıköy" },
    },
    {
      type: "Feature",
      geometry: { type: "Point", coordinates: [29, 41] },
      properties: { id: 2, name: "Cihangir", districtId: 11, districtName: "Beyoğlu" },
    },
    {
      type: "Feature",
      geometry: { type: "Polygon", coordinates: [] },
      properties: { id: 3, name: "Invalide", districtId: 12, districtName: "Test" },
    },
  ],
};

function buildMetricValues(): MetricValues {
  return {
    population_density: {
      "1": { raw: 42, normalized: 0, qualityFlag: "estimated" },
      "2": { raw: 4200, normalized: 1, qualityFlag: "ok" },
    },
  };
}

describe("choropleth feature collection", () => {
  it("joins metric values and keeps a normalized zero", () => {
    const collection = buildChoroplethFeatureCollection({
      neighborhoods,
      metricValues: buildMetricValues(),
      metricKey: "population_density",
    });

    expect(collection.features).toHaveLength(1);
    expect(collection.features[0].properties).toEqual({
      id: 1,
      name: "Moda",
      districtName: "Kadıköy",
      raw: 42,
      normalized: 0,
      qualityFlag: "estimated",
    });
  });

  it("skips non-polygon and invalid geometries", () => {
    const collection = buildChoroplethFeatureCollection({
      neighborhoods,
      metricValues: buildMetricValues(),
      metricKey: "population_density",
    });

    expect(collection.features.map((feature) => feature.properties.name)).toEqual(["Moda"]);
  });

  it("marks neighborhoods without a value as missing", () => {
    const collection = buildChoroplethFeatureCollection({
      neighborhoods,
      metricValues: {},
      metricKey: "population_density",
    });

    expect(collection.features[0].properties.raw).toBeNull();
    expect(collection.features[0].properties.normalized).toBeNull();
    expect(collection.features[0].properties.qualityFlag).toBeNull();
  });

  it("selects a feature by id and returns nothing without one", () => {
    const collection: ChoroplethFeatureCollection = buildChoroplethFeatureCollection({
      neighborhoods,
      metricValues: buildMetricValues(),
      metricKey: "population_density",
    });

    expect(selectFeaturesById(collection, 1).features).toHaveLength(1);
    expect(selectFeaturesById(collection, 2).features).toHaveLength(0);
    expect(selectFeaturesById(collection, null).features).toHaveLength(0);
  });
  it("splits neighborhoods with and without a normalized value", () => {
    const collection: ChoroplethFeatureCollection = {
      type: "FeatureCollection",
      features: [
        {
          type: "Feature",
          geometry: { type: "Polygon", coordinates: MODA_POLYGON },
          properties: {
            id: 1,
            name: "Moda",
            districtName: "Kadıköy",
            raw: 42,
            normalized: 0.5,
            qualityFlag: "estimated",
          },
        },
        {
          type: "Feature",
          geometry: { type: "Polygon", coordinates: MODA_POLYGON },
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

    const { withValue, withoutValue } = splitFeaturesByMetricValue(collection);

    expect(withValue.features.map((feature) => feature.properties.name)).toEqual(["Moda"]);
    expect(withoutValue.features.map((feature) => feature.properties.name)).toEqual(["Cihangir"]);
  });

  it("computes the bounds of every polygon and returns null when empty", () => {
    const collection = buildChoroplethFeatureCollection({
      neighborhoods,
      metricValues: buildMetricValues(),
      metricKey: "population_density",
    });

    expect(collectionBounds(collection)).toEqual([
      [29, 41],
      [29.01, 41.01],
    ]);
    expect(collectionBounds({ type: "FeatureCollection", features: [] })).toBeNull();
  });
});
