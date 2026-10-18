import { describe, expect, it } from "vite-plus/test";
import {
  DEFAULT_WEIGHT,
  EMPTY_CRITERIA,
  hasCriterion,
  isMetricKey,
  toggleCriterion,
  weightOf,
  withWeight,
  withoutCriterion,
  type Criteria,
} from "./criteria";

const CAFES_AND_METRO: Criteria = {
  weights: [
    { metricKey: "cafes", weight: 0.6 },
    { metricKey: "metro", weight: 0.2 },
  ],
};

describe("criterion weights", () => {
  it("adds a missing criterion with the given weight", () => {
    expect(withWeight(EMPTY_CRITERIA, "cafes", 0.6)).toEqual({
      weights: [{ metricKey: "cafes", weight: 0.6 }],
    });
  });

  it("updates the weight of an existing criterion in place", () => {
    const updated = withWeight(CAFES_AND_METRO, "metro", 0.9);

    expect(updated).toEqual({
      weights: [
        { metricKey: "cafes", weight: 0.6 },
        { metricKey: "metro", weight: 0.9 },
      ],
    });
  });

  it("keeps the state identical when the weight does not change", () => {
    expect(withWeight(CAFES_AND_METRO, "cafes", 0.6)).toBe(CAFES_AND_METRO);
  });

  it("removes the criterion when the weight reaches zero", () => {
    expect(withWeight(CAFES_AND_METRO, "cafes", 0)).toEqual({
      weights: [{ metricKey: "metro", weight: 0.2 }],
    });
  });

  it("ignores a weight that is not a finite number", () => {
    expect(withWeight(CAFES_AND_METRO, "cafes", Number.NaN)).toBe(CAFES_AND_METRO);
    expect(withWeight(CAFES_AND_METRO, "cafes", Number.POSITIVE_INFINITY)).toBe(CAFES_AND_METRO);
  });

  it("clamps the weight to the allowed range", () => {
    expect(weightOf(withWeight(EMPTY_CRITERIA, "cafes", 4), "cafes")).toBe(1);
    expect(weightOf(withWeight(EMPTY_CRITERIA, "cafes", 0.604), "cafes")).toBe(0.6);
  });

  it("ignores a metric key that is not a slug", () => {
    expect(withWeight(EMPTY_CRITERIA, "Cafés", 0.5)).toBe(EMPTY_CRITERIA);
    expect(withWeight(EMPTY_CRITERIA, "", 0.5)).toBe(EMPTY_CRITERIA);
    expect(withWeight(EMPTY_CRITERIA, "x".repeat(33), 0.5)).toBe(EMPTY_CRITERIA);
  });
});

describe("criterion lookups", () => {
  it("reads the weight of a criterion and nothing for a missing one", () => {
    expect(weightOf(CAFES_AND_METRO, "cafes")).toBe(0.6);
    expect(weightOf(CAFES_AND_METRO, "quiet")).toBeUndefined();
    expect(hasCriterion(CAFES_AND_METRO, "cafes")).toBe(true);
    expect(hasCriterion(CAFES_AND_METRO, "quiet")).toBe(false);
  });

  it("toggles a criterion on and off", () => {
    const on = toggleCriterion(EMPTY_CRITERIA, "quiet");

    expect(on).toEqual({ weights: [{ metricKey: "quiet", weight: DEFAULT_WEIGHT }] });
    expect(toggleCriterion(on, "quiet")).toEqual(EMPTY_CRITERIA);
  });

  it("removes a criterion and keeps the remaining order", () => {
    expect(withoutCriterion(CAFES_AND_METRO, "cafes")).toEqual({
      weights: [{ metricKey: "metro", weight: 0.2 }],
    });
    expect(withoutCriterion(CAFES_AND_METRO, "quiet")).toBe(CAFES_AND_METRO);
  });

  it("accepts a bounded lower-case slug as a metric key", () => {
    expect(isMetricKey("quiet")).toBe(true);
    expect(isMetricKey("nearby-parks")).toBe(true);
    expect(isMetricKey("Cafes")).toBe(false);
    expect(isMetricKey("1cafes")).toBe(false);
    expect(isMetricKey("cafes;drop")).toBe(false);
  });
});
