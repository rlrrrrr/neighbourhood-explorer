import { describe, expect, it } from "vite-plus/test";
import { selectedMetricSort } from "./metric-sort-choice";

const METRICS = [
  { key: "greenery", polarity: "higher_is_better" },
  { key: "noise", polarity: "lower_is_better" },
  { key: "land_use_diversity", polarity: "neutral" },
] as const;

describe("metric sort choice", () => {
  it("puts the best value first according to the metric polarity", () => {
    expect(selectedMetricSort({ metricKey: "greenery", metrics: METRICS })).toEqual({
      key: "greenery",
      direction: "descending",
    });
    expect(selectedMetricSort({ metricKey: "noise", metrics: METRICS })).toEqual({
      key: "noise",
      direction: "ascending",
    });
    expect(selectedMetricSort({ metricKey: "land_use_diversity", metrics: METRICS })).toEqual({
      key: "land_use_diversity",
      direction: "descending",
    });
  });

  it("returns no sort for the alphabetical choice or an unpublished metric", () => {
    expect(selectedMetricSort({ metricKey: "", metrics: METRICS })).toBeNull();
    expect(selectedMetricSort({ metricKey: "nightlife", metrics: METRICS })).toBeNull();
  });
});
