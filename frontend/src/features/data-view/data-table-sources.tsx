import { localizedMetricName, shortLicense } from "../metrics/metric-presentation";
import type { MetricDefinition } from "../metrics/metric-api-schema";

export function DataTableSources({
  metrics,
  locale,
}: {
  metrics: readonly MetricDefinition[];
  locale: string;
}) {
  return (
    <>
      <ul className="mt-5 flex flex-wrap gap-x-4 gap-y-1 text-xs text-text-secondary">
        {metrics.map((metric) => (
          <li key={metric.key}>
            {localizedMetricName(metric, locale)} · {metric.source.organization} ·{" "}
            {shortLicense(metric.source.license)} · {metric.observedAt ?? "—"}
          </li>
        ))}
      </ul>

      <details className="mt-5 border-t border-border pt-4 text-xs text-text-secondary">
        <summary className="cursor-pointer font-semibold text-text">
          <fbt desc="Summary of the sources and dates panel under the data table">
            Sources et dates
          </fbt>
        </summary>
        <ul className="mt-3 flex flex-col gap-4">
          {metrics.map((metric) => (
            <li key={metric.key}>
              <p className="font-semibold text-text">{localizedMetricName(metric, locale)}</p>
              <p className="mt-1">
                {metric.source.name} — {metric.source.organization}
              </p>
              <p className="mt-1">{metric.source.attribution}</p>
              <p className="mt-1">{metric.source.license}</p>
              <p className="mt-1">{metric.source.methodology}</p>
              <p className="mt-1">
                <fbt desc="Observation date label in the source panel">Observation :</fbt>{" "}
                {metric.observedAt ?? "—"} ·{" "}
                <fbt desc="Retrieval date label in the source panel">Récupération :</fbt>{" "}
                {metric.source.retrievedAt}
              </p>
              <a
                href={metric.source.url}
                target="_blank"
                rel="noreferrer"
                className="mt-1 block break-all underline"
              >
                {metric.source.url}
              </a>
            </li>
          ))}
        </ul>
      </details>
    </>
  );
}
