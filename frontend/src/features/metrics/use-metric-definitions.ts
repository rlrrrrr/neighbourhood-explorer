import { useQuery } from "@tanstack/react-query";
import { fetchMetricDefinitions } from "./metric-definitions-client";
import type { MetricDefinition } from "./metric-api-schema";

const EMPTY_METRICS: readonly MetricDefinition[] = [];

export const metricDefinitionsQueryKey = ["metric-definitions"] as const;

export function useMetricDefinitions(): readonly MetricDefinition[] {
  const query = useQuery({
    queryKey: metricDefinitionsQueryKey,
    queryFn: ({ signal }) => fetchMetricDefinitions(signal),
  });
  return query.data ?? EMPTY_METRICS;
}
