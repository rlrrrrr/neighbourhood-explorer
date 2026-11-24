import { describe, expect, it } from "vite-plus/test";
import {
  metricValuesSchema,
  metricsResponseSchema,
  neighborhoodCollectionSchema,
} from "../metrics/metric-api-schema";
import { createMapData, fetchMapStatic } from "./map-data-client";

const metricsPayload = {
  metrics: [
    {
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
        name: "WorldPop 2025",
        organization: "WorldPop",
        url: "https://www.worldpop.org/",
        license: "CC BY 4.0",
        attribution: "WorldPop 2025",
        methodology: "Estimation",
        retrievedAt: "2026-01-01",
        version: "2025",
      },
      observedAt: "2025-12-31",
    },
    {
      key: "nightlife",
      category: "lifestyle",
      unit: "places/km²",
      nativeLevel: "neighborhood",
      polarity: "higher_is_better",
      normalization: { method: "percentile", version: "2" },
      displayPrecision: 1,
      translations: [
        {
          locale: "fr",
          name: "Vie nocturne",
          description: "Établissements ouverts le soir par kilomètre carré.",
          subject: "Vie nocturne",
        },
      ],
      source: {
        id: "osm-nightlife",
        name: "OpenStreetMap 2025",
        organization: "OpenStreetMap contributors",
        url: "https://www.openstreetmap.org/",
        license: "ODbL 1.0",
        attribution: "OpenStreetMap contributors",
        methodology: "Comptage des lieux",
        retrievedAt: "2026-01-01",
        version: "2025",
      },
      observedAt: "2025-12-31",
    },
  ],
};

const valuesPayload = {
  population_density: {
    "1": { raw: 42, normalized: 0.4, qualityFlag: "estimated" },
  },
  nightlife: {
    "1": { raw: 7, normalized: 0.9, qualityFlag: "ok" },
  },
};

const neighborhoodsPayload = {
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
      properties: { id: 1, name: "Caferağa Mahallesi", districtId: 10, districtName: "Kadıköy" },
    },
  ],
};

function jsonResponse(payload: unknown, status = 200): Response {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { "content-type": "application/json" },
  });
}

async function withFetch(
  fetchReplacement: (input: RequestInfo | URL) => Promise<Response>,
  run: () => Promise<void>,
): Promise<void> {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (input) => fetchReplacement(input);

  try {
    await run();
  } finally {
    globalThis.fetch = originalFetch;
  }
}

function requestUrl(input: RequestInfo | URL): string {
  if (typeof input === "string") {
    return input;
  }

  return input instanceof URL ? input.toString() : input.url;
}

function respondFromFixtures(input: RequestInfo | URL): Promise<Response> {
  const url = requestUrl(input);

  if (url.endsWith("/api/v1/metrics")) {
    return Promise.resolve(jsonResponse(metricsPayload));
  }

  if (url.includes("/metric-values")) {
    return Promise.resolve(jsonResponse(valuesPayload));
  }

  return Promise.resolve(jsonResponse(neighborhoodsPayload));
}

function parsedFixtures() {
  return {
    metrics: metricsResponseSchema.parse(metricsPayload).metrics,
    neighborhoods: neighborhoodCollectionSchema.parse(neighborhoodsPayload),
    values: metricValuesSchema.parse(valuesPayload),
  };
}

describe("map data client", () => {
  it("fetches definitions and geometries without metric values", async () => {
    const urls: string[] = [];

    await withFetch(
      (input) => {
        urls.push(requestUrl(input));

        return respondFromFixtures(input);
      },
      async () => {
        const staticData = await fetchMapStatic({
          cityId: 34,
          signal: new AbortController().signal,
        });

        expect(staticData.metrics.map((definition) => definition.key)).toEqual([
          "population_density",
          "nightlife",
        ]);
        expect(staticData.neighborhoods.features).toHaveLength(1);
        expect(urls).toHaveLength(2);
        expect(urls.some((url) => url.includes("/metric-values"))).toBe(false);
      },
    );
  });

  it("switches the displayed metric from the cached values, without any request", () => {
    const fixtures = parsedFixtures();

    const population = createMapData({
      ...fixtures,
      metricKey: "population_density",
      fallbackMetricKey: "population_density",
    });

    const nightlife = createMapData({
      ...fixtures,
      metricKey: "nightlife",
      fallbackMetricKey: "population_density",
    });

    expect(population?.metric.key).toBe("population_density");
    expect(population?.collection.features[0].properties).toEqual({
      id: 1,
      name: "Caferağa Mahallesi",
      districtName: "Kadıköy",
      raw: 42,
      normalized: 0.4,
      qualityFlag: "estimated",
    });
    expect(nightlife?.metric.key).toBe("nightlife");
    expect(nightlife?.collection.features[0].properties).toEqual({
      id: 1,
      name: "Caferağa Mahallesi",
      districtName: "Kadıköy",
      raw: 7,
      normalized: 0.9,
      qualityFlag: "ok",
    });
  });

  it("falls back to the default metric when the requested key is not published", () => {
    const data = createMapData({
      ...parsedFixtures(),
      metricKey: "not_published",
      fallbackMetricKey: "population_density",
    });

    expect(data?.metric.key).toBe("population_density");
    expect(data?.collection.features).toHaveLength(1);
  });

  it("has nothing to display when no metric is published", () => {
    expect(
      createMapData({
        ...parsedFixtures(),
        metrics: [],
        metricKey: "population_density",
        fallbackMetricKey: "population_density",
      }),
    ).toBeNull();
  });

  it("rejects a metric definition without its source methodology", async () => {
    const incompleteMetrics = {
      metrics: [
        {
          ...metricsPayload.metrics[0],
          source: { ...metricsPayload.metrics[0].source, methodology: undefined },
        },
      ],
    };

    await withFetch(
      (input) =>
        requestUrl(input).endsWith("/api/v1/metrics")
          ? Promise.resolve(jsonResponse(incompleteMetrics))
          : respondFromFixtures(input),
      async () => {
        await expect(
          fetchMapStatic({ cityId: 34, signal: new AbortController().signal }),
        ).rejects.toMatchObject({ code: "invalid_response" });
      },
    );
  });
});
