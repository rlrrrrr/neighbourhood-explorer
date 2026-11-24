import { getApiJson } from "../metrics/api-json";
import { fetchCityMetricValues } from "../metrics/city-metric-values";
import {
  metricsResponseSchema,
  neighborhoodCollectionSchema,
  type MetricDefinition,
  type MetricValue,
  type MetricValues,
  type NeighborhoodCollection,
} from "../metrics/metric-api-schema";

export type DataTableValue = {
  raw: number;
  qualityFlag: MetricValue["qualityFlag"];
};

export type DataTableRow = {
  neighborhoodId: number;
  name: string;
  districtName: string;
  values: Readonly<Record<string, DataTableValue>>;
};

export type DataTableData = {
  metrics: readonly MetricDefinition[];
  rows: readonly DataTableRow[];
};

export async function fetchDataTable({
  cityId,
  signal,
}: {
  cityId: number;
  signal: AbortSignal;
}): Promise<DataTableData> {
  const metricsPayload = await getApiJson({
    path: "/api/v1/metrics",
    schema: metricsResponseSchema,
    signal,
  });

  const metrics = metricsPayload.metrics;

  if (metrics.length === 0) {
    return { metrics, rows: [] };
  }

  const [neighborhoodsPayload, valuesPayload] = await Promise.all([
    getApiJson({
      path: `/api/v1/cities/${cityId}/neighborhoods`,
      schema: neighborhoodCollectionSchema,
      signal,
    }),
    fetchCityMetricValues({ cityId, metricKeys: metrics.map((metric) => metric.key), signal }),
  ]);

  return {
    metrics,
    rows: buildRows({ neighborhoodsPayload, valuesPayload }),
  };
}

function buildRows({
  neighborhoodsPayload,
  valuesPayload,
}: {
  neighborhoodsPayload: NeighborhoodCollection;
  valuesPayload: MetricValues;
}): DataTableRow[] {
  return neighborhoodsPayload.features.map((feature) => ({
    neighborhoodId: feature.properties.id,
    name: feature.properties.name,
    districtName: feature.properties.districtName,
    values: readValues({ valuesPayload, neighborhoodId: feature.properties.id }),
  }));
}

function readValues({
  valuesPayload,
  neighborhoodId,
}: {
  valuesPayload: MetricValues;
  neighborhoodId: number;
}): Record<string, DataTableValue> {
  const values: Record<string, DataTableValue> = {};

  for (const [metricKey, valuesByNeighborhood] of Object.entries(valuesPayload)) {
    const value = valuesByNeighborhood[String(neighborhoodId)];

    if (value !== undefined) {
      values[metricKey] = { raw: value.raw, qualityFlag: value.qualityFlag };
    }
  }

  return values;
}
