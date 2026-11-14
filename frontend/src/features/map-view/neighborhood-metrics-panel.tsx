import { useMemo, useState } from "react";
import { fbs } from "fbtee";
import { Button } from "../../components/ui/button";
import { formatMetricValue, metricQualityLabel } from "../metrics/metric-presentation";
import {
  filterNeighborhoodMetricRows,
  type NeighborhoodMetricRow,
} from "./neighborhood-metric-rows";
import type { NeighborhoodMetricsState } from "./use-neighborhood-metrics";

export interface NeighborhoodMetricsPanelProps {
  neighborhoodName: string;
  districtName: string;
  rows: readonly NeighborhoodMetricRow[];
  state: NeighborhoodMetricsState;
  locale: string;
  onReload: () => void;
}

export function NeighborhoodMetricsPanel({
  neighborhoodName,
  districtName,
  rows,
  state,
  locale,
  onReload,
}: NeighborhoodMetricsPanelProps) {
  const [query, setQuery] = useState("");
  const visibleRows = useMemo(
    () => filterNeighborhoodMetricRows({ rows, query, locale }),
    [rows, query, locale],
  );

  return (
    <section aria-labelledby="neighborhood-metrics-title" className="rounded-xl bg-surface p-3">
      <h3 id="neighborhood-metrics-title" className="text-sm font-bold">
        {neighborhoodName}
      </h3>
      <p className="text-xs text-text-secondary">{districtName}</p>

      {state.status === "loading" ? (
        <p role="status" className="mt-3 text-xs text-text-secondary">
          <fbt desc="Shown while every metric of the selected neighborhood is loading">
            Chargement des données du quartier…
          </fbt>
        </p>
      ) : null}

      {state.status === "error" ? (
        <div className="mt-3">
          <p className="text-xs text-text-secondary">
            <fbt desc="Shown when the metrics of the selected neighborhood cannot be loaded">
              Les données du quartier n'ont pas pu être chargées.
            </fbt>
          </p>
          <Button size="sm" variant="outline" className="mt-2" onClick={onReload}>
            <fbt desc="Retry loading the selected neighborhood metrics">Réessayer</fbt>
          </Button>
        </div>
      ) : null}

      {state.status === "ready" ? (
        <>
          <label className="mt-2 block">
            <span className="sr-only">
              <fbt desc="Label of the field filtering the selected neighborhood metrics by name">
                Filtrer les métriques
              </fbt>
            </span>
            <input
              type="search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={fbs(
                "Filtrer les métriques",
                "Placeholder of the field filtering the neighborhood metrics by name",
              )}
              className="min-h-9 w-full rounded-lg border border-border bg-bg px-2 text-xs text-text focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
            />
          </label>
          <p role="status" className="mt-1 text-[10px] text-text-secondary">
            <fbt desc="Count of metrics shown in the selected neighborhood panel">
              Métriques affichées :
            </fbt>{" "}
            <span className="tabular-nums">
              {visibleRows.length} / {rows.length}
            </span>
            {" · "}
            <fbt desc="Hint explaining that a metric row opens its source and date">
              détails au clic
            </fbt>
          </p>

          <ul className="mt-1">
            {visibleRows.map((row) => (
              <NeighborhoodMetricLine key={row.key} row={row} locale={locale} />
            ))}
          </ul>

          {visibleRows.length === 0 ? (
            <p role="status" className="mt-2 text-xs text-text-secondary">
              <fbt desc="Shown when the neighborhood metric filter matches nothing">
                Aucune métrique ne correspond à ce filtre.
              </fbt>
            </p>
          ) : null}
        </>
      ) : null}

      {state.status === "idle" ? (
        <p className="mt-3 text-xs text-text-secondary">
          <fbt desc="Hint shown when no neighborhood is selected on the map">
            Clique un quartier sur la carte ou choisis-le dans la liste.
          </fbt>
        </p>
      ) : null}
    </section>
  );
}

function NeighborhoodMetricLine({ row, locale }: { row: NeighborhoodMetricRow; locale: string }) {
  const ratio = row.normalized === null ? null : Math.min(Math.max(row.normalized, 0), 1);

  return (
    <li className="border-t border-border/60 first:border-t-0">
      <details className="group">
        <summary className="relative grid cursor-pointer list-none grid-cols-[minmax(0,1fr)_auto] items-baseline gap-x-2 py-1 pb-2.5 [&::-webkit-details-marker]:hidden">
          <span className="line-clamp-2 min-w-0 text-xs font-medium leading-tight">{row.name}</span>
          <span className="shrink-0 text-right">
            <MetricValue row={row} locale={locale} />
            {row.qualityFlag === null || row.qualityFlag === "ok" ? null : (
              <span className="block text-[10px] font-normal text-text-secondary">
                {metricQualityLabel(row.qualityFlag)}
              </span>
            )}
          </span>
          <span
            aria-hidden="true"
            className="absolute inset-x-0 bottom-1 h-0.5 overflow-hidden rounded-full bg-border/40"
          >
            <span
              className="block h-full rounded-full bg-action"
              style={{ width: `${Math.round((ratio ?? 0) * 100)}%` }}
            />
          </span>
        </summary>
        <div className="pb-2 pt-1 text-[10px] text-text-secondary">
          <p>
            {row.category} ·{" "}
            {row.sourceName ?? (
              <fbt desc="Shown when a neighborhood metric has no source name">Source inconnue</fbt>
            )}
          </p>
          <p>
            <fbt desc="Observation date label of a neighborhood metric">Observation :</fbt>{" "}
            <bdi dir="ltr">{row.observedAt ?? "—"}</bdi>
            {row.referencePeriod === null ? null : (
              <>
                {" · "}
                <fbt desc="Reference period label of a neighborhood metric">Période :</fbt>{" "}
                <bdi dir="ltr">{row.referencePeriod}</bdi>
              </>
            )}
          </p>
          {ratio === null ? null : (
            <p>
              <fbt desc="Label of the normalized value drawn as a bar under the metric">
                Valeur normalisée :
              </fbt>{" "}
              <bdi dir="ltr">
                {formatMetricValue({ value: ratio, locale, maximumFractionDigits: 2 })}
              </bdi>
            </p>
          )}
        </div>
      </details>
    </li>
  );
}

function MetricValue({ row, locale }: { row: NeighborhoodMetricRow; locale: string }) {
  if (row.raw === null) {
    return (
      <>
        <span aria-hidden="true" className="text-xs text-text-secondary">
          —
        </span>
        <span className="sr-only">
          <fbt desc="Shown when a selected neighborhood has no value for a metric">
            Pas de donnée
          </fbt>
        </span>
      </>
    );
  }

  return (
    <bdi dir="ltr">
      <span className="text-xs font-semibold tabular-nums">
        {formatMetricValue({
          value: row.raw,
          locale,
          maximumFractionDigits: row.displayPrecision,
        })}
      </span>{" "}
      <span className="text-[10px] font-normal text-text-secondary">{row.unit}</span>
    </bdi>
  );
}
