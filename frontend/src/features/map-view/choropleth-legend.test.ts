import { describe, expect, it } from "vite-plus/test";
import type { ChoroplethFeatureCollection, ChoroplethProperties } from "./choropleth";
import { buildLegendStops } from "./choropleth-legend";

function feature(properties: Partial<ChoroplethProperties> & { id: number }): {
  type: "Feature";
  geometry: { type: "Polygon"; coordinates: [number, number][][] };
  properties: ChoroplethProperties;
} {
  return {
    type: "Feature",
    geometry: {
      type: "Polygon",
      coordinates: [
        [
          [0, 0],
          [0, 1],
          [1, 1],
          [0, 0],
        ],
      ],
    },
    properties: {
      name: `Neighborhood ${properties.id}`,
      districtName: "District",
      raw: null,
      normalized: null,
      qualityFlag: null,
      ...properties,
    },
  };
}

function collection(
  features: ChoroplethFeatureCollection["features"],
): ChoroplethFeatureCollection {
  return { type: "FeatureCollection", features };
}

describe("choropleth legend stops", () => {
  it("labels each ramp stop with the raw value at the same normalized position", () => {
    const stops = buildLegendStops(
      collection([
        feature({ id: 1, raw: 10, normalized: 0 }),
        feature({ id: 2, raw: 20, normalized: 0.25 }),
        feature({ id: 3, raw: 30, normalized: 0.5 }),
        feature({ id: 4, raw: 40, normalized: 0.75 }),
        feature({ id: 5, raw: 50, normalized: 1 }),
      ]),
    );

    expect(stops).toEqual([10, 20, 30, 40, 50]);
  });

  it("chooses the closest normalized value for each stop", () => {
    const stops = buildLegendStops(
      collection([
        feature({ id: 1, raw: 99, normalized: 0.95 }),
        feature({ id: 2, raw: 12, normalized: 0.1 }),
        feature({ id: 3, raw: 61, normalized: 0.6 }),
      ]),
    );

    expect(stops).toEqual([12, 12, 61, 61, 99]);
  });

  it("keeps the zeros of a zero-inflated metric on the low stops", () => {
    const stops = buildLegendStops(
      collection([
        feature({ id: 1, raw: 0, normalized: 0 }),
        feature({ id: 2, raw: 0, normalized: 0 }),
        feature({ id: 3, raw: 0, normalized: 0 }),
        feature({ id: 4, raw: 0, normalized: 0 }),
        feature({ id: 5, raw: 0, normalized: 0 }),
        feature({ id: 6, raw: 3, normalized: 1 }),
      ]),
    );

    expect(stops).toEqual([0, 0, 0, 3, 3]);
  });

  it("returns no value for every stop when nothing is measured", () => {
    const stops = buildLegendStops(collection([feature({ id: 1 })]));

    expect(stops).toEqual([null, null, null, null, null]);
  });
});
