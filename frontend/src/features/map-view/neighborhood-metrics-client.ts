import { ApiRequestError, getApiJson } from "../metrics/api-json";
import { isNeighborhoodMetrics, type NeighborhoodMetrics } from "../metrics/metric-api-schema";

export async function fetchNeighborhoodMetrics({
  neighborhoodId,
  signal,
}: {
  neighborhoodId: number;
  signal: AbortSignal;
}): Promise<NeighborhoodMetrics> {
  const payload = await getApiJson(`/api/v1/neighborhoods/${neighborhoodId}/metrics`, signal);
  if (!isNeighborhoodMetrics(payload)) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: "La réponse des métriques du quartier est invalide.",
    });
  }
  return payload;
}
