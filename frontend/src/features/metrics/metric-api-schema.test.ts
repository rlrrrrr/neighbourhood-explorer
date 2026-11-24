import { describe, expect, it } from "vite-plus/test";
import {
  metricValuesSchema,
  metricsResponseSchema,
  neighborhoodCollectionSchema,
  problemDetailsSchema,
} from "./metric-api-schema";

const metricDefinition = {
  key: "greenery",
  category: "Environment",
  unit: "%",
  nativeLevel: "neighborhood",
  polarity: "higher_is_better",
  normalization: { method: "percentile", version: "percentile-v1" },
  displayPrecision: 1,
  translations: [
    {
      locale: "fr",
      name: "Verdure",
      description: "Part de la surface verte.",
      subject: "la verdure",
    },
  ],
  source: {
    id: "esa-worldcover",
    name: "ESA WorldCover 2021",
    organization: "ESA WorldCover consortium",
    url: "https://esa-worldcover.org/",
    license: "CC BY 4.0",
    attribution: "© ESA WorldCover project 2021",
    methodology: "Part de la surface classée verte.",
    retrievedAt: "2026-11-09",
    version: "WorldCover 2021 v200",
  },
  observedAt: null,
};

describe("API response schemas", () => {
  it("accepts a valid metric definition and strips undeclared keys at the boundary", () => {
    const parsed = metricsResponseSchema.parse({
      metrics: [{ ...metricDefinition, unexpected: "ignored" }],
    });

    expect(parsed.metrics[0]).not.toHaveProperty("unexpected");
    expect(parsed.metrics[0].key).toBe("greenery");
  });

  it("rejects a translation whose locale is not published by the API", () => {
    const parsed = metricsResponseSchema.safeParse({
      metrics: [
        {
          ...metricDefinition,
          translations: [{ ...metricDefinition.translations[0], locale: "ar" }],
        },
      ],
    });

    expect(parsed.success).toBe(false);
  });

  it("rejects an unknown quality flag", () => {
    const parsed = metricValuesSchema.safeParse({
      greenery: { "1272": { raw: 2, normalized: 0.1, qualityFlag: "unknown" } },
    });

    expect(parsed.success).toBe(false);
  });

  it("rejects a non-numeric raw value", () => {
    const parsed = metricValuesSchema.safeParse({
      greenery: { "1272": { raw: "2", normalized: 0.1, qualityFlag: "ok" } },
    });

    expect(parsed.success).toBe(false);
  });

  it("rejects a GeoJSON payload with the wrong collection type", () => {
    const parsed = neighborhoodCollectionSchema.safeParse({ type: "Feature", features: [] });

    expect(parsed.success).toBe(false);
  });

  it("accepts an empty problem details object because every field is optional", () => {
    expect(problemDetailsSchema.parse({})).toEqual({});
  });
});
