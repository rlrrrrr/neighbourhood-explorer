import type { MetricDefinition } from "../metrics/metric-api-schema";

export function listMetricCategories(metrics: readonly MetricDefinition[]): readonly string[] {
  return [...new Set(metrics.map((metric) => metric.category))];
}

export function filterMetricsByCategory({
  metrics,
  selectedCategories,
  currentMetricKey,
}: {
  metrics: readonly MetricDefinition[];
  selectedCategories: ReadonlySet<string>;
  currentMetricKey: string;
}): readonly MetricDefinition[] {
  if (selectedCategories.size === 0) {
    return metrics;
  }
  return metrics.filter(
    (metric) => selectedCategories.has(metric.category) || metric.key === currentMetricKey,
  );
}
