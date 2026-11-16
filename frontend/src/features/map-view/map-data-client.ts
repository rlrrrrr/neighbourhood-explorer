import { buildChoroplethFeatureCollection, type ChoroplethFeatureCollection } from "./choropleth";
import { ApiRequestError, getApiJson } from "../metrics/api-json";
import {
  isMetricValues,
  isMetricsResponse,
  isNeighborhoodCollection,
  type MetricDefinition,
} from "../metrics/metric-api-schema";

export type MapData = {
  collection: ChoroplethFeatureCollection;
  metric: MetricDefinition;
  metrics: readonly MetricDefinition[];
};

export async function fetchMapData({
  cityId,
  metricKey,
  fallbackMetricKey,
  signal,
}: {
  cityId: number;
  metricKey: string;
  fallbackMetricKey: string;
  signal: AbortSignal;
}): Promise<MapData> {
  const requestedMetricKeys = [...new Set([metricKey, fallbackMetricKey])];

  const encodedMetricKeys = requestedMetricKeys
    .map((requestedKey) => encodeURIComponent(requestedKey))
    .join(",");

  const [metricsPayload, valuesPayload, neighborhoodsPayload] = await Promise.all([
    getApiJson("/api/v1/metrics", signal),
    getApiJson(`/api/v1/cities/${cityId}/metric-values?metrics=${encodedMetricKeys}`, signal),
    getApiJson(`/api/v1/cities/${cityId}/neighborhoods`, signal),
  ]);

  if (!isMetricsResponse(metricsPayload)) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: "La réponse de /api/v1/metrics est invalide.",
    });
  }

  const metric =
    metricsPayload.metrics.find((candidate) => candidate.key === metricKey) ??
    metricsPayload.metrics.find((candidate) => candidate.key === fallbackMetricKey);

  if (metric === undefined) {
    throw new ApiRequestError({
      code: "metric_not_found",
      message: `La métrique ${metricKey} n'est pas publiée par l'API.`,
    });
  }

  if (!isMetricValues(valuesPayload)) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: "La réponse des valeurs de métriques est invalide.",
    });
  }

  if (!isNeighborhoodCollection(neighborhoodsPayload)) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: "La réponse GeoJSON des quartiers est invalide.",
    });
  }

  return {
    metric,
    metrics: metricsPayload.metrics,
    collection: buildChoroplethFeatureCollection({
      neighborhoods: neighborhoodsPayload,
      metricValues: valuesPayload,
      metricKey: metric.key,
    }),
  };
}
