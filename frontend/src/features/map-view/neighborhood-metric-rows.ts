import type {
  MetricDefinition,
  MetricValue,
  NeighborhoodMetric,
} from "../metrics/metric-api-schema";
import {
  localizedCategory,
  localizedMetricName,
  localizedUnit,
} from "../metrics/metric-presentation";

export type NeighborhoodMetricRow = {
  key: string;
  name: string;
  category: string;
  unit: string;
  displayPrecision: number;
  raw: number | null;
  normalized: number | null;
  qualityFlag: MetricValue["qualityFlag"] | null;
  observedAt: string | null;
  referencePeriod: string | null;
  sourceName: string | null;
};

export function buildNeighborhoodMetricRows({
  definitions,
  metrics,
  locale,
}: {
  definitions: readonly MetricDefinition[];
  metrics: readonly NeighborhoodMetric[];
  locale: string;
}): NeighborhoodMetricRow[] {
  const valuesByKey = new Map(metrics.map((metric) => [metric.key, metric]));

  return definitions.map((definition) => {
    const value = valuesByKey.get(definition.key);
    return {
      key: definition.key,
      name: localizedMetricName(definition, locale),
      category: localizedCategory(definition.category),
      unit: localizedUnit(definition.unit),
      displayPrecision: definition.displayPrecision,
      raw: value?.raw ?? null,
      normalized: value?.normalized ?? null,
      qualityFlag: value?.qualityFlag ?? null,
      observedAt: value?.observedAt ?? definition.observedAt,
      referencePeriod: value?.referencePeriod ?? null,
      sourceName: value?.source.name ?? definition.source.name,
    };
  });
}

export function filterNeighborhoodMetricRows({
  rows,
  query,
  locale,
}: {
  rows: readonly NeighborhoodMetricRow[];
  query: string;
  locale: string;
}): NeighborhoodMetricRow[] {
  const needle = query.trim().toLocaleLowerCase(locale);
  if (needle === "") {
    return [...rows];
  }
  return rows.filter((row) => row.name.toLocaleLowerCase(locale).includes(needle));
}
