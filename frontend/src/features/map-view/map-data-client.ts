import { buildChoroplethFeatureCollection, type ChoroplethFeatureCollection } from "./choropleth";
import { getApiJson } from "../metrics/api-json";
import {
  metricsResponseSchema,
  neighborhoodCollectionSchema,
  type MetricDefinition,
  type MetricValues,
  type NeighborhoodCollection,
} from "../metrics/metric-api-schema";

export type MapData = {
  collection: ChoroplethFeatureCollection;
  metric: MetricDefinition;
  metrics: readonly MetricDefinition[];
};

export type MapStaticData = {
  metrics: readonly MetricDefinition[];
  neighborhoods: NeighborhoodCollection;
};

export async function fetchMapStatic({
  cityId,
  signal,
}: {
  cityId: number;
  signal: AbortSignal;
}): Promise<MapStaticData> {
  const [metricsPayload, neighborhoodsPayload] = await Promise.all([
    getApiJson({ path: "/api/v1/metrics", schema: metricsResponseSchema, signal }),
    getApiJson({
      path: `/api/v1/cities/${cityId}/neighborhoods`,
      schema: neighborhoodCollectionSchema,
      signal,
    }),
  ]);

  return { metrics: metricsPayload.metrics, neighborhoods: neighborhoodsPayload };
}

export function createMapData({
  metrics,
  neighborhoods,
  values,
  metricKey,
  fallbackMetricKey,
}: {
  metrics: readonly MetricDefinition[];
  neighborhoods: NeighborhoodCollection;
  values: MetricValues;
  metricKey: string;
  fallbackMetricKey: string;
}): MapData | null {
  const metric =
    metrics.find((candidate) => candidate.key === metricKey) ??
    metrics.find((candidate) => candidate.key === fallbackMetricKey);

  if (metric === undefined) {
    return null;
  }

  return {
    metric,
    metrics,
    collection: buildChoroplethFeatureCollection({
      neighborhoods,
      metricValues: values,
      metricKey: metric.key,
    }),
  };
}
