import { getApiJson } from "../metrics/api-json";
import { neighborhoodMetricsSchema, type NeighborhoodMetrics } from "../metrics/metric-api-schema";

export async function fetchNeighborhoodMetrics({
  neighborhoodId,
  signal,
}: {
  neighborhoodId: number;
  signal: AbortSignal;
}): Promise<NeighborhoodMetrics> {
  return getApiJson({
    path: `/api/v1/neighborhoods/${neighborhoodId}/metrics`,
    schema: neighborhoodMetricsSchema,
    signal,
  });
}
