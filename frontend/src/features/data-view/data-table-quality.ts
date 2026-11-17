import type { DataTableRow, DataTableValue } from "./data-table-client";

export type MetricQualityFlag = DataTableValue["qualityFlag"];

export function dominantQualityFlags({
  rows,
  metricKeys,
}: {
  rows: readonly DataTableRow[];
  metricKeys: readonly string[];
}): ReadonlyMap<string, MetricQualityFlag> {
  const countsByMetric = new Map<string, Map<MetricQualityFlag, number>>();

  for (const row of rows) {
    for (const metricKey of metricKeys) {
      const value = row.values[metricKey];

      if (value === undefined) {
        continue;
      }

      const counts = countsByMetric.get(metricKey);

      if (counts === undefined) {
        countsByMetric.set(metricKey, new Map([[value.qualityFlag, 1]]));
        continue;
      }

      counts.set(value.qualityFlag, (counts.get(value.qualityFlag) ?? 0) + 1);
    }
  }

  const dominant = new Map<string, MetricQualityFlag>();

  for (const [metricKey, counts] of countsByMetric) {
    let dominantFlag: MetricQualityFlag | null = null;
    let dominantCount = 0;

    for (const [flag, count] of counts) {
      if (count > dominantCount) {
        dominantFlag = flag;
        dominantCount = count;
      }
    }

    if (dominantFlag !== null) {
      dominant.set(metricKey, dominantFlag);
    }
  }

  return dominant;
}
