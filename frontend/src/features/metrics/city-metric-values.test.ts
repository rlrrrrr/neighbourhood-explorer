import { describe, expect, it } from "vite-plus/test";
import { ApiRequestError } from "./api-json";
import { fetchCityMetricValues } from "./city-metric-values";

const valuesPayload = {
  population_density: {
    "1": { raw: 42, normalized: 0.4, qualityFlag: "estimated" },
  },
  nightlife: {
    "1": { raw: 7, normalized: 0.9, qualityFlag: "ok" },
  },
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

describe("city metric values client", () => {
  it("requests every metric of the city once, without a duplicate key", async () => {
    const urls: string[] = [];

    await withFetch(
      (input) => {
        urls.push(requestUrl(input));

        return Promise.resolve(jsonResponse(valuesPayload));
      },
      async () => {
        const values = await fetchCityMetricValues({
          cityId: 34,
          metricKeys: ["population_density", "nightlife", "nightlife"],
          signal: new AbortController().signal,
        });

        expect(Object.keys(values)).toEqual(["population_density", "nightlife"]);
        expect(urls).toEqual([
          "/api/v1/cities/34/metric-values?metrics=population_density,nightlife",
        ]);
      },
    );
  });

  it("carries the API problem code when a request fails", async () => {
    await withFetch(
      () =>
        Promise.resolve(jsonResponse({ code: "ai_disabled", detail: "L'IA est désactivée." }, 503)),
      async () => {
        const rejection = fetchCityMetricValues({
          cityId: 34,
          metricKeys: ["population_density"],
          signal: new AbortController().signal,
        });

        await expect(rejection).rejects.toBeInstanceOf(ApiRequestError);
        await expect(rejection).rejects.toMatchObject({ code: "ai_disabled" });
      },
    );
  });
});
