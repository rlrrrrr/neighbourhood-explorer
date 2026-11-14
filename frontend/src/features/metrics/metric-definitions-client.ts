import { ApiRequestError, getApiJson } from "./api-json";
import { isMetricsResponse, type MetricDefinition } from "./metric-api-schema";

export async function fetchMetricDefinitions(
  signal: AbortSignal,
): Promise<readonly MetricDefinition[]> {
  const payload = await getApiJson("/api/v1/metrics", signal);
  if (!isMetricsResponse(payload)) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: "La réponse de /api/v1/metrics est invalide.",
    });
  }
  return payload.metrics;
}
