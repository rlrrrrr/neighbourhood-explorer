import { describe, expect, it } from "vite-plus/test";
import { EMPTY_CRITERIA, type Criteria } from "./criteria";
import {
  decodeCriteria,
  encodeCriteria,
  MAX_CRITERIA_COUNT,
  MAX_CRITERIA_LENGTH,
} from "./criteria-url";

const STATES: ReadonlyArray<[string, Criteria]> = [
  ["one default weight", { weights: [{ metricKey: "quiet", weight: 1 }] }],
  [
    "several weights",
    {
      weights: [
        { metricKey: "quiet", weight: 1 },
        { metricKey: "cafes", weight: 0.6 },
        { metricKey: "metro", weight: 0.25 },
      ],
    },
  ],
];

describe("criteria encoding", () => {
  it("writes the default weight as the bare metric key", () => {
    expect(encodeCriteria({ weights: [{ metricKey: "quiet", weight: 1 }] })).toBe("quiet");
    expect(
      encodeCriteria({
        weights: [
          { metricKey: "cafes", weight: 0.6 },
          { metricKey: "metro", weight: 1 },
        ],
      }),
    ).toBe("cafes:0.6,metro");
  });

  it("encodes nothing when no criterion is set", () => {
    expect(encodeCriteria(EMPTY_CRITERIA)).toBeUndefined();
  });

  it("keeps the declaration order of the criteria", () => {
    expect(
      encodeCriteria({
        weights: [
          { metricKey: "metro", weight: 1 },
          { metricKey: "cafes", weight: 1 },
        ],
      }),
    ).toBe("metro,cafes");
  });
});

describe("criteria decoding", () => {
  it("reads the bare metric key as the default weight", () => {
    expect(decodeCriteria("quiet")).toEqual({ weights: [{ metricKey: "quiet", weight: 1 }] });
    expect(decodeCriteria("cafes:0.6,metro")).toEqual({
      weights: [
        { metricKey: "cafes", weight: 0.6 },
        { metricKey: "metro", weight: 1 },
      ],
    });
  });

  it("rejects anything that is not a list of bounded slugs", () => {
    expect(decodeCriteria(undefined)).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria(42)).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("   ")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("Cafés")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("cafes;drop")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("x".repeat(MAX_CRITERIA_LENGTH + 1))).toBe(EMPTY_CRITERIA);
  });

  it("drops an item whose weight is out of range or unreadable", () => {
    expect(decodeCriteria("quiet:0")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("quiet:-1")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("quiet:2")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("quiet:abc")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("quiet:0.6:0.2")).toBe(EMPTY_CRITERIA);
    expect(decodeCriteria("quiet:0.6, ,cafes:2")).toEqual({
      weights: [{ metricKey: "quiet", weight: 0.6 }],
    });
  });

  it("rounds a weight to two decimals", () => {
    expect(decodeCriteria("quiet:0.666")).toEqual({
      weights: [{ metricKey: "quiet", weight: 0.67 }],
    });
  });

  it("keeps the first weight of a metric listed twice", () => {
    expect(decodeCriteria("quiet:0.4,quiet:0.9")).toEqual({
      weights: [{ metricKey: "quiet", weight: 0.4 }],
    });
  });

  it("bounds the number of criteria", () => {
    const many = Array.from({ length: MAX_CRITERIA_COUNT + 5 }, (_, index) => `metric-${index}`);

    expect(decodeCriteria(many.join(",")).weights).toHaveLength(MAX_CRITERIA_COUNT);
  });
});

describe("criteria round trip", () => {
  it("rebuilds the same state through the encoded value", () => {
    for (const [name, state] of STATES) {
      const encoded = encodeCriteria(state);
      expect(encoded, name).toBeDefined();
      expect(decodeCriteria(encoded), name).toEqual(state);
      expect(encodeCriteria(decodeCriteria(encoded)), name).toBe(encoded);
    }
  });

  it("rebuilds the same state from a hand-written value", () => {
    for (const value of ["quiet", "cafes:0.6,metro", "metro,cafes:0.25"]) {
      expect(encodeCriteria(decodeCriteria(value))).toBe(value);
    }
  });
});
