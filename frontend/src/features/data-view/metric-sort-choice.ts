import type { MetricDefinition } from "../metrics/metric-api-schema";
import type { TableSort } from "./table-sort";

function bestFirstDirection(polarity: MetricDefinition["polarity"]): TableSort["direction"] {
  return polarity === "lower_is_better" ? "ascending" : "descending";
}

export function selectedMetricSort({
  metricKey,
  metrics,
}: {
  metricKey: string;
  metrics: ReadonlyArray<{ key: string; polarity: MetricDefinition["polarity"] }>;
}): TableSort | null {
  const metric = metrics.find((candidate) => candidate.key === metricKey);

  return metric === undefined
    ? null
    : { key: metric.key, direction: bestFirstDirection(metric.polarity) };
}
