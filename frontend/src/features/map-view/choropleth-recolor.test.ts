import { afterEach, describe, expect, it, vi } from "vite-plus/test";
import type { Feature, Polygon } from "geojson";
import type { ChoroplethFeatureCollection, ChoroplethProperties } from "./choropleth";
import {
  CHOROPLETH_RECOLOR_MS,
  choroplethRecolorDuration,
  easeOutProgress,
  interpolateChoroplethNormalized,
  withPreviousNormalized,
} from "./choropleth-recolor";

const GEOMETRY: Polygon = {
  type: "Polygon",
  coordinates: [
    [
      [29, 41],
      [29, 41.01],
      [29.01, 41.01],
      [29, 41],
    ],
  ],
};

function feature(
  id: number,
  normalized: number | null,
  raw: number | null = normalized,
): Feature<Polygon, ChoroplethProperties> {
  return {
    type: "Feature",
    geometry: GEOMETRY,
    properties: {
      id,
      name: `Quartier ${id}`,
      districtName: "Kadıköy",
      raw,
      normalized,
      qualityFlag: null,
    },
  };
}

function collection(...features: Array<Feature<Polygon, ChoroplethProperties>>) {
  return { type: "FeatureCollection", features } satisfies ChoroplethFeatureCollection;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("choropleth recolor", () => {
  it("morphs normalized values while keeping the target raw data", () => {
    const result = interpolateChoroplethNormalized({
      from: collection(feature(1, 0), feature(2, 1)),
      to: collection(feature(1, 1, 120), feature(2, 0, 30)),
      progress: 0.25,
    });

    expect(result.features[0].properties.normalized).toBeCloseTo(0.25);
    expect(result.features[0].properties.raw).toBe(120);
    expect(result.features[1].properties.normalized).toBeCloseTo(0.75);
    expect(result.features[1].properties.raw).toBe(30);
  });

  it("leaves features without a target value untouched", () => {
    const withoutValue = feature(2, null, null);

    const result = interpolateChoroplethNormalized({
      from: collection(feature(1, 0), feature(2, 0)),
      to: collection(feature(1, 1), withoutValue),
      progress: 0.5,
    });

    expect(result.features[1]).toBe(withoutValue);
  });

  it("starts at the previous value and reaches the target", () => {
    const from = collection(feature(1, 0.2));
    const to = collection(feature(1, 0.8));

    expect(
      interpolateChoroplethNormalized({ from, to, progress: 0 }).features[0].properties.normalized,
    ).toBeCloseTo(0.2);
    expect(
      interpolateChoroplethNormalized({ from, to, progress: 1 }).features[0].properties.normalized,
    ).toBeCloseTo(0.8);
  });

  it("keeps the previous normalized value alongside the new one", () => {
    const blended = withPreviousNormalized({
      previous: collection(feature(1, 0.2), feature(2, null, null)),
      next: collection(feature(1, 0.8), feature(2, 0.4)),
    });

    expect(blended.features[0].properties.previousNormalized).toBeCloseTo(0.2);
    expect(blended.features[0].properties.normalized).toBeCloseTo(0.8);
    expect(blended.features[1].properties.previousNormalized).toBeNull();
  });

  it("front-loads the motion", () => {
    expect(easeOutProgress(0)).toBe(0);
    expect(easeOutProgress(1)).toBe(1);
    expect(easeOutProgress(0.5)).toBeGreaterThan(0.5);
  });

  it("animates for 220 ms by default", () => {
    vi.stubGlobal("window", { matchMedia: () => ({ matches: false }) });

    expect(choroplethRecolorDuration()).toBe(CHOROPLETH_RECOLOR_MS);
  });

  it("jumps immediately when reduced motion is requested", () => {
    vi.stubGlobal("window", { matchMedia: () => ({ matches: true }) });

    expect(choroplethRecolorDuration()).toBe(0);
  });
});
