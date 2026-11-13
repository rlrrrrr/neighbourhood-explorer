import { describe, expect, it } from "vite-plus/test";
import { DataTableRequestError, fetchDataTable } from "./data-table-client";

const metricsPayload = {
  metrics: [
    {
      key: "greenery",
      category: "Environment",
      unit: "% de la surface",
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
      observedAt: "2021-12-31",
    },
    {
      key: "cafes_density",
      category: "Lifestyle",
      unit: "cafés/km²",
      nativeLevel: "neighborhood",
      polarity: "higher_is_better",
      normalization: { method: "percentile", version: "percentile-v1" },
      displayPrecision: 1,
      translations: [
        {
          locale: "fr",
          name: "Densité de cafés",
          description: "Cafés par kilomètre carré.",
          subject: "les cafés",
        },
      ],
      source: {
        id: "osm-geofabrik-istanbul",
        name: "OpenStreetMap",
        organization: "OpenStreetMap contributors",
        url: "https://www.openstreetmap.org/",
        license: "ODbL 1.0",
        attribution: "© les contributeurs d'OpenStreetMap",
        methodology: "Cafés OSM par kilomètre carré.",
        retrievedAt: "2026-10-31",
        version: "turkey-261030.osm.pbf",
      },
      observedAt: "2026-10-30",
    },
  ],
};

const valuesPayload = {
  greenery: {
    "1272": { raw: 2, normalized: 0.1, qualityFlag: "estimated" },
  },
  cafes_density: {
    "1272": { raw: 196.2, normalized: 0.9, qualityFlag: "ok" },
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
      properties: { id: 1272, name: "Cihangir Mahallesi", districtId: 10, districtName: "Beyoğlu" },
    },
    {
      type: "Feature",
      geometry: {
        type: "Polygon",
        coordinates: [
          [
            [28.98, 41.03],
            [28.98, 41.04],
            [28.99, 41.04],
            [28.98, 41.03],
          ],
        ],
      },
      properties: { id: 1570, name: "Caferağa Mahallesi", districtId: 11, districtName: "Kadıköy" },
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

describe("data table client", () => {
  it("joins the definitions, values and neighborhoods in the requested order", async () => {
    await withFetch(respondFromFixtures, async () => {
      const data = await fetchDataTable({
        cityId: 34,
        metricKeys: ["greenery", "cafes_density", "quiet"],
        signal: new AbortController().signal,
      });

      expect(data.metrics.map((metric) => metric.key)).toEqual(["greenery", "cafes_density"]);
      expect(data.unknownMetricKeys).toEqual(["quiet"]);
      expect(data.rows).toEqual([
        {
          neighborhoodId: 1272,
          name: "Cihangir Mahallesi",
          districtName: "Beyoğlu",
          values: {
            greenery: { raw: 2, qualityFlag: "estimated" },
            cafes_density: { raw: 196.2, qualityFlag: "ok" },
          },
        },
        {
          neighborhoodId: 1570,
          name: "Caferağa Mahallesi",
          districtName: "Kadıköy",
          values: {},
        },
      ]);
    });
  });

  it("requests values only for the metrics published by the API", async () => {
    const requests: string[] = [];

    await withFetch(
      (input) => {
        requests.push(requestUrl(input));
        return respondFromFixtures(input);
      },
      async () => {
        await fetchDataTable({
          cityId: 34,
          metricKeys: ["greenery", "quiet"],
          signal: new AbortController().signal,
        });
      },
    );

    const valuesRequest = requests.find((url) => url.includes("/metric-values"));
    expect(valuesRequest).toContain("metrics=greenery");
    expect(valuesRequest).not.toContain("quiet");
  });

  it("does not download the neighborhoods when no active criterion is published", async () => {
    const requests: string[] = [];

    await withFetch(
      (input) => {
        requests.push(requestUrl(input));
        return respondFromFixtures(input);
      },
      async () => {
        const data = await fetchDataTable({
          cityId: 34,
          metricKeys: ["quiet"],
          signal: new AbortController().signal,
        });

        expect(data).toEqual({ metrics: [], rows: [], unknownMetricKeys: ["quiet"] });
      },
    );

    expect(requests.some((url) => url.includes("/neighborhoods"))).toBe(false);
    expect(requests.some((url) => url.includes("/metric-values"))).toBe(false);
  });

  it("does not call the API when no metric is active", async () => {
    await withFetch(
      () => Promise.reject(new Error("no request expected")),
      async () => {
        const data = await fetchDataTable({
          cityId: 34,
          metricKeys: [],
          signal: new AbortController().signal,
        });

        expect(data).toEqual({ metrics: [], rows: [], unknownMetricKeys: [] });
      },
    );
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
          fetchDataTable({
            cityId: 34,
            metricKeys: ["greenery"],
            signal: new AbortController().signal,
          }),
        ).rejects.toMatchObject({ code: "invalid_response" });
      },
    );
  });

  it("carries the API problem code when a request fails", async () => {
    await withFetch(
      () =>
        Promise.resolve(
          jsonResponse({ code: "database_unavailable", detail: "La base est indisponible." }, 503),
        ),
      async () => {
        const rejection = fetchDataTable({
          cityId: 34,
          metricKeys: ["greenery"],
          signal: new AbortController().signal,
        });

        await expect(rejection).rejects.toBeInstanceOf(DataTableRequestError);
        await expect(rejection).rejects.toMatchObject({ code: "database_unavailable" });
      },
    );
  });
});
