import type { components } from "../../generated/api";
import { buildChoroplethFeatureCollection, type ChoroplethFeatureCollection } from "./choropleth";

const API_BASE_URL = import.meta.env.VITE_API_URL ?? "";
const QUALITY_FLAGS = new Set(["ok", "estimated", "stale", "low_coverage", "inherited"]);

export type MapData = {
  collection: ChoroplethFeatureCollection;
  metric: components["schemas"]["MetricDefinition"];
};

export class MapDataRequestError extends Error {
  readonly code: string;

  constructor({ code, message }: { code: string; message: string }) {
    super(message);
    this.name = "MapDataRequestError";
    this.code = code;
  }
}

type MetricDefinition = components["schemas"]["MetricDefinition"];
type MetricsResponse = components["schemas"]["MetricsResponse"];
type MetricValues = components["schemas"]["MetricValues"];
type NeighborhoodCollection = components["schemas"]["NeighborhoodFeatureCollection"];
type ProblemDetails = components["schemas"]["ProblemDetails"];

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

function isMetricsResponse(value: unknown): value is MetricsResponse {
  return isRecord(value) && Array.isArray(value.metrics) && value.metrics.every(isMetricDefinition);
}

function isMetricDefinition(value: unknown): value is MetricDefinition {
  return (
    isRecord(value) &&
    typeof value.key === "string" &&
    typeof value.category === "string" &&
    typeof value.unit === "string" &&
    (value.nativeLevel === "province" ||
      value.nativeLevel === "district" ||
      value.nativeLevel === "neighborhood") &&
    (value.polarity === "higher_is_better" ||
      value.polarity === "lower_is_better" ||
      value.polarity === "neutral") &&
    isRecord(value.normalization) &&
    typeof value.normalization.method === "string" &&
    typeof value.normalization.version === "string" &&
    typeof value.displayPrecision === "number" &&
    (value.observedAt === null || typeof value.observedAt === "string") &&
    Array.isArray(value.translations) &&
    value.translations.every(
      (translation) =>
        isRecord(translation) &&
        typeof translation.locale === "string" &&
        typeof translation.name === "string",
    ) &&
    isRecord(value.source) &&
    isMetricSource(value.source)
  );
}

function isMetricSource(value: Record<string, unknown>): value is MetricDefinition["source"] {
  return (
    typeof value.id === "string" &&
    typeof value.name === "string" &&
    typeof value.organization === "string" &&
    typeof value.url === "string" &&
    typeof value.license === "string" &&
    typeof value.attribution === "string" &&
    typeof value.methodology === "string" &&
    typeof value.retrievedAt === "string" &&
    typeof value.version === "string"
  );
}

function isMetricValues(value: unknown): value is MetricValues {
  return (
    isRecord(value) &&
    Object.values(value).every(
      (valuesByNeighborhood) =>
        isRecord(valuesByNeighborhood) && Object.values(valuesByNeighborhood).every(isMetricValue),
    )
  );
}

function isMetricValue(value: unknown): value is components["schemas"]["MetricValue"] {
  return (
    isRecord(value) &&
    typeof value.raw === "number" &&
    typeof value.normalized === "number" &&
    typeof value.qualityFlag === "string" &&
    QUALITY_FLAGS.has(value.qualityFlag)
  );
}

function isNeighborhoodCollection(value: unknown): value is NeighborhoodCollection {
  return (
    isRecord(value) &&
    value.type === "FeatureCollection" &&
    Array.isArray(value.features) &&
    value.features.every(
      (feature) =>
        isRecord(feature) &&
        feature.type === "Feature" &&
        isRecord(feature.geometry) &&
        typeof feature.geometry.type === "string" &&
        Array.isArray(feature.geometry.coordinates) &&
        isRecord(feature.properties) &&
        typeof feature.properties.id === "number" &&
        typeof feature.properties.name === "string" &&
        typeof feature.properties.districtId === "number" &&
        typeof feature.properties.districtName === "string",
    )
  );
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    isRecord(value) &&
    (value.title === undefined || typeof value.title === "string") &&
    (value.detail === undefined || typeof value.detail === "string") &&
    (value.code === undefined || typeof value.code === "string")
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
