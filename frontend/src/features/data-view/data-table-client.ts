import { ApiRequestError, getApiJson } from "../metrics/api-json";
import {
  isMetricValues,
  isMetricsResponse,
  isNeighborhoodCollection,
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
  const metricsPayload = await getApiJson("/api/v1/metrics", signal);

  if (!isMetricsResponse(metricsPayload)) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: "La réponse de /api/v1/metrics est invalide.",
    });
  }

  const metrics = metricsPayload.metrics;
  if (metrics.length === 0) {
    return { metrics, rows: [] };
  }

  const [neighborhoodsPayload, valuesPayload] = await Promise.all([
    getApiJson(`/api/v1/cities/${cityId}/neighborhoods`, signal),
    fetchMetricValues({ cityId, metricKeys: metrics.map((metric) => metric.key), signal }),
  ]);

  if (!isNeighborhoodCollection(neighborhoodsPayload)) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: "La réponse GeoJSON des quartiers est invalide.",
    });
  }

  return {
    metrics,
    rows: buildRows({ neighborhoodsPayload, valuesPayload }),
  };
}

async function fetchMetricValues({
  cityId,
  metricKeys,
  signal,
}: {
  cityId: number;
  metricKeys: readonly string[];
  signal: AbortSignal;
}): Promise<MetricValues> {
  const encodedKeys = metricKeys.map((metricKey) => encodeURIComponent(metricKey)).join(",");
  const payload = await getApiJson(
    `/api/v1/cities/${cityId}/metric-values?metrics=${encodedKeys}`,
    signal,
  );

  if (!isMetricValues(payload)) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: "La réponse des valeurs de métriques est invalide.",
    });
  }

  return payload;
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
