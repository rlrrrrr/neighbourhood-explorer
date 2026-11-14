import { ChevronDown } from "lucide-react";
import type { MetricDefinition } from "../features/metrics/metric-api-schema";
import { localizedMetricName } from "../features/metrics/metric-presentation";

interface MetricFilterProps {
  metrics: readonly MetricDefinition[];
  selectedKey: string | null;
  locale: string;
  onChange: (metricKey: string | null) => void;
}

export function MetricFilter({ metrics, selectedKey, locale, onChange }: MetricFilterProps) {
  return (
    <div className="group relative min-w-0">
      <label htmlFor="metric-filter" className="sr-only">
        <fbt desc="Accessible label of the shared metric filter in the header">
          Métrique de classement
        </fbt>
      </label>
      <select
        id="metric-filter"
        value={selectedKey ?? ""}
        onChange={(event) => onChange(event.target.value === "" ? null : event.target.value)}
        className="min-h-11 max-w-44 appearance-none truncate rounded-full border border-border bg-transparent py-1 ps-3 pe-8 text-xs font-semibold text-text transition-[border-color] duration-[160ms] ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action sm:min-h-9 sm:max-w-52 [&:open]:border-action"
      >
        <option value="">
          <fbt desc="Option of the shared metric filter that disables metric ranking">
            Toutes les métriques
          </fbt>
        </option>
        {metrics.map((metric) => (
          <option key={metric.key} value={metric.key}>
            {localizedMetricName(metric, locale)}
          </option>
        ))}
      </select>
      <ChevronDown
        aria-hidden="true"
        className="pointer-events-none absolute end-2.5 top-1/2 size-3.5 -translate-y-1/2 text-text-secondary transition-transform duration-[160ms] ease-out group-has-[select:open]:rotate-180 motion-reduce:transition-none"
      />
    </div>
  );
}
