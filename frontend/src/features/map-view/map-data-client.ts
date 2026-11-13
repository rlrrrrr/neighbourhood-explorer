import { buildChoroplethFeatureCollection, type ChoroplethFeatureCollection } from "./choropleth";
import {
  isMetricValues,
  isMetricsResponse,
  isNeighborhoodCollection,
  isProblemDetails,
  type MetricDefinition,
  type ProblemDetails,
} from "../metrics/metric-api-schema";

const API_BASE_URL = import.meta.env.VITE_API_URL ?? "";

export type MapData = {
  collection: ChoroplethFeatureCollection;
  metric: MetricDefinition;
};

export class MapDataRequestError extends Error {
  readonly code: string;

  constructor({ code, message }: { code: string; message: string }) {
    super(message);
    this.name = "MapDataRequestError";
    this.code = code;
  }
}

export async function fetchMapData({
  cityId,
  metricKey,
  signal,
}: {
  cityId: number;
  metricKey: string;
  signal: AbortSignal;
}): Promise<MapData> {
  const encodedMetricKey = encodeURIComponent(metricKey);
  const [metricsPayload, valuesPayload, neighborhoodsPayload] = await Promise.all([
    getJson("/api/v1/metrics", signal),
    getJson(`/api/v1/cities/${cityId}/metric-values?metrics=${encodedMetricKey}`, signal),
    getJson(`/api/v1/cities/${cityId}/neighborhoods`, signal),
  ]);

  if (!isMetricsResponse(metricsPayload)) {
    throw new MapDataRequestError({
      code: "invalid_response",
      message: "La réponse de /api/v1/metrics est invalide.",
    });
  }

  const metric = metricsPayload.metrics.find((candidate) => candidate.key === metricKey);
  if (metric === undefined) {
    throw new MapDataRequestError({
      code: "metric_not_found",
      message: `La métrique ${metricKey} n'est pas publiée par l'API.`,
    });
  }

  if (!isMetricValues(valuesPayload)) {
    throw new MapDataRequestError({
      code: "invalid_response",
      message: "La réponse des valeurs de métriques est invalide.",
    });
  }

  if (!isNeighborhoodCollection(neighborhoodsPayload)) {
    throw new MapDataRequestError({
      code: "invalid_response",
      message: "La réponse GeoJSON des quartiers est invalide.",
    });
  }

  return {
    metric,
    collection: buildChoroplethFeatureCollection({
      neighborhoods: neighborhoodsPayload,
      metricValues: valuesPayload,
      metricKey,
    }),
  };
}

async function getJson(path: string, signal: AbortSignal): Promise<unknown> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    signal,
    headers: { Accept: "application/json, application/geo+json" },
  });

  if (!response.ok) {
    const problem = await readProblemDetails(response);
    throw new MapDataRequestError({
      code: problem?.code ?? `http_${response.status}`,
      message: problem?.detail ?? `L'API a répondu ${response.status}.`,
    });
  }

  return response.json();
}

async function readProblemDetails(response: Response): Promise<ProblemDetails | null> {
  try {
    const payload: unknown = await response.json();
    return isProblemDetails(payload) ? payload : null;
  } catch {
    return null;
  }
}
