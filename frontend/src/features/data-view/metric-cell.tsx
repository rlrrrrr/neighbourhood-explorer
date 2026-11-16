import {
  formatMetricValue,
  localizedUnit,
  metricQualityLabel,
} from "../metrics/metric-presentation";
import type { MetricDefinition } from "../metrics/metric-api-schema";
import type { DataTableValue } from "./data-table-client";

export function MetricCell({
  value,
  metric,
  locale,
  highlighted,
}: {
  value: DataTableValue | undefined;
  metric: MetricDefinition;
  locale: string;
  highlighted: boolean;
}) {
  if (value === undefined) {
    return (
      <>
        <span aria-hidden="true" className="font-mono text-xs text-text-secondary">
          —
        </span>
        <span className="sr-only">
          <fbt desc="Shown when a neighborhood has no value for a metric">Pas de donnée</fbt>
        </span>
      </>
    );
  }

  const formatted = formatMetricValue({
    value: value.raw,
    locale,
    maximumFractionDigits: metric.displayPrecision,
  });

  return (
    <bdi dir="ltr">
      {highlighted ? (
        <span className="inline-flex h-5 items-center justify-center rounded-full bg-action px-2 font-mono text-xs font-medium text-action-text tabular-nums">
          {formatted}
        </span>
      ) : (
        <span className="font-mono text-xs text-text-secondary tabular-nums">{formatted}</span>
      )}
      <span className="sr-only"> {localizedUnit(metric.unit)}</span>
      {value.qualityFlag === "ok" ? null : (
        <span className="block font-sans text-[10px] font-normal text-text-secondary">
          {metricQualityLabel(value.qualityFlag)}
        </span>
      )}
    </bdi>
  );
}
