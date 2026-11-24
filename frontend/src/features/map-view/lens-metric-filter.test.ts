import { describe, expect, it } from "vite-plus/test";
import type { MetricDefinition } from "../metrics/metric-api-schema";
import { filterMetricsByCategory, listMetricCategories } from "./lens-metric-filter";

function metric(key: string, category: string): MetricDefinition {
  return {
    key,
    category,
    unit: "unit",
    nativeLevel: "neighborhood",
    polarity: "neutral",
    normalization: { method: "min_max", version: "1" },
    displayPrecision: 0,
    translations: [
      {
        locale: "fr",
        name: key,
        description: key,
        subject: key,
      },
    ],
    source: {
      id: "source",
      name: "Source",
      organization: "Organization",
      url: "https://example.test/",
      license: "ODbL 1.0",
      attribution: "Attribution",
      methodology: "Methodology",
      retrievedAt: "2026-01-01",
      version: "1",
    },
    observedAt: "2025-12-31",
  };
}

const METRICS = [
  metric("population_density", "People"),
  metric("cafes_density", "Lifestyle"),
  metric("greenery", "Environment"),
  metric("metro_stations", "Mobility"),
  metric("nightlife_density", "Lifestyle"),
];

function filteredKeys({
  categories,
  currentMetricKey,
}: {
  categories: readonly string[];
  currentMetricKey: string;
}): readonly string[] {
  return filterMetricsByCategory({
    metrics: METRICS,
    selectedCategories: new Set(categories),
    currentMetricKey,
  }).map((definition) => definition.key);
}

describe("lens metric filter", () => {
  it("lists each published category once, in the order the API returns it", () => {
    expect(listMetricCategories(METRICS)).toEqual([
      "People",
      "Lifestyle",
      "Environment",
      "Mobility",
    ]);
  });

  it("lists every metric while no category is selected", () => {
    expect(filteredKeys({ categories: [], currentMetricKey: "greenery" })).toEqual(
      METRICS.map((definition) => definition.key),
    );
  });

  it("keeps only the metrics of the selected categories", () => {
    expect(filteredKeys({ categories: ["Lifestyle"], currentMetricKey: "greenery" })).toEqual([
      "cafes_density",
      "greenery",
      "nightlife_density",
    ]);
  });

  it("keeps the metric displayed on the map even when its category is filtered out", () => {
    expect(
      filteredKeys({ categories: ["Environment"], currentMetricKey: "cafes_density" }),
    ).toEqual(["cafes_density", "greenery"]);
  });

  it("returns nothing when no metric matches the selected categories", () => {
    expect(filteredKeys({ categories: ["Health"], currentMetricKey: "" })).toEqual([]);
  });
});
