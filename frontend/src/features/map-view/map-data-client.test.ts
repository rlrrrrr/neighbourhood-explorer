import { describe, expect, it } from "vite-plus/test";
import { fetchMapData, MapDataRequestError } from "./map-data-client";

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
  ],
};

const valuesPayload = {
  population_density: {
    "1": { raw: 42, normalized: 0.4, qualityFlag: "estimated" },
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

describe("map data client", () => {
  it("joins the metric definition, values and neighborhoods", async () => {
    await withFetch(respondFromFixtures, async () => {
      const data = await fetchMapData({
        cityId: 34,
        metricKey: "population_density",
        signal: new AbortController().signal,
      });

      expect(data.metric.key).toBe("population_density");
      expect(data.collection.features[0].properties).toEqual({
        id: 1,
        name: "Caferağa Mahallesi",
        districtName: "Kadıköy",
        raw: 42,
        normalized: 0.4,
        qualityFlag: "estimated",
      });
    });
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
          fetchMapData({
            cityId: 34,
            metricKey: "population_density",
            signal: new AbortController().signal,
          }),
        ).rejects.toMatchObject({ code: "invalid_response" });
      },
    );
  });

  it("carries the API problem code when a request fails", async () => {
    await withFetch(
      () =>
        Promise.resolve(jsonResponse({ code: "ai_disabled", detail: "L'IA est désactivée." }, 503)),
      async () => {
        const rejection = fetchMapData({
          cityId: 34,
          metricKey: "population_density",
          signal: new AbortController().signal,
        });

        await expect(rejection).rejects.toBeInstanceOf(MapDataRequestError);
        await expect(rejection).rejects.toMatchObject({ code: "ai_disabled" });
      },
    );
  });
});
