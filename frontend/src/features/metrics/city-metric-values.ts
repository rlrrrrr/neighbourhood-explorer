import { getApiJson } from "./api-json";
import { metricValuesSchema, type MetricValues } from "./metric-api-schema";

export async function fetchCityMetricValues({
  cityId,
  metricKeys,
  signal,
}: {
  cityId: number;
  metricKeys: readonly string[];
  signal: AbortSignal;
}): Promise<MetricValues> {
  const encodedMetricKeys = [...new Set(metricKeys)].map(encodeURIComponent).join(",");

  return getApiJson({
    path: `/api/v1/cities/${cityId}/metric-values?metrics=${encodedMetricKeys}`,
    schema: metricValuesSchema,
    signal,
  });
}
