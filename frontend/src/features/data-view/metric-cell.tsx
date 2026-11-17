import { cn } from "../../components/ui/cn";
import {
  formatMetricValue,
  localizedUnit,
  metricQualityLabel,
} from "../metrics/metric-presentation";
import type { MetricDefinition } from "../metrics/metric-api-schema";
import type { DataTableValue } from "./data-table-client";
import type { MetricQualityFlag } from "./data-table-quality";

export function MetricCell({
  value,
  metric,
  locale,
  highlighted,
  featured,
  dominantQualityFlag,
}: {
  value: DataTableValue | undefined;
  metric: MetricDefinition;
  locale: string;
  highlighted: boolean;
  featured: boolean;
  dominantQualityFlag: MetricQualityFlag | undefined;
}) {
  if (value === undefined) {
    return (
      <>
        <span aria-hidden="true" className="text-xs text-text-secondary">
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
    <>
      <bdi dir="ltr">
        {featured ? (
          <span className="inline-flex h-5 items-center rounded bg-action px-2 text-[11px] font-medium text-action-text tabular-nums">
            {formatted}
          </span>
        ) : (
          <span
            className={cn(
              "text-xs tabular-nums",
              highlighted ? "font-semibold text-text" : "text-text-secondary",
            )}
          >
            {formatted}
          </span>
        )}
        <span className="sr-only"> {localizedUnit(metric.unit)}</span>
      </bdi>
      {value.qualityFlag === dominantQualityFlag ? null : (
        <span className="ms-1 text-[10px] text-text-secondary">
          {metricQualityLabel(value.qualityFlag)}
        </span>
      )}
    </>
  );
}
