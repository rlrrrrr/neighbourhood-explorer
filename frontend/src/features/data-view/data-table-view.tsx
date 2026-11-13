import { useMemo } from "react";
import { Button } from "../../components/ui/button";
import { cn } from "../../components/ui/cn";
import { useScrollOverflowHint } from "../../components/ui/scroll-affordance";
import { CriteriaChips } from "../criteria/criteria-chips";
import type { Criteria } from "../criteria/criteria";
import {
  formatMetricValue,
  localizedMetricName,
  localizedUnit,
  metricQualityLabel,
  shortLicense,
} from "../metrics/metric-presentation";
import { nextSort, sortRows, type TableSort } from "./table-sort";
import type { DataTableData, DataTableRow } from "./data-table-client";
import type { DataTableState } from "./use-data-table";

interface DataTableViewProps {
  criteria: Criteria;
  state: DataTableState;
  sort: TableSort | null;
  locale: string;
  onRemoveCriterion: (metricKey: string) => void;
  onSortChange: (sort: TableSort) => void;
  onShowOnMap: (neighborhoodId: number) => void;
  onReload: () => void;
}

export function DataTableView({
  criteria,
  state,
  sort,
  locale,
  onRemoveCriterion,
  onSortChange,
  onShowOnMap,
  onReload,
}: DataTableViewProps) {
  const { containerRef, hasMore, overflows } = useScrollOverflowHint({ axis: "horizontal" });
  const data = state.status === "ready" ? state.data : null;
  const metricNames = useMemo(
    () =>
      new Map(
        (data?.metrics ?? []).map((metric) => [metric.key, localizedMetricName(metric, locale)]),
      ),
    [data, locale],
  );
  const orderedRows = useMemo(
    () => (data === null ? [] : orderRows({ data, sort, locale })),
    [data, sort, locale],
  );
  const stickyColumn = cn("sticky start-0 z-10 bg-inherit", overflows && "border-e");

  return (
    <div className="mx-auto w-full max-w-4xl flex-1 px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight">
        <fbt desc="Title of the data view">Valeurs brutes</fbt>
      </h1>
      <p className="mt-2 text-sm text-text-secondary">
        <fbt desc="Subtitle of the data view, promising raw values with their unit, date and source">
          Valeurs brutes avec unité, date d'observation et source, sans score composite
        </fbt>
      </p>

      {criteria.weights.length > 0 && (
        <section className="mt-6" aria-labelledby="data-criteria-label">
          <h2 id="data-criteria-label" className="text-xs font-semibold text-text-secondary">
            <fbt desc="Title of the active criteria group above the raw values table">
              Critères actifs
            </fbt>
          </h2>
          <div className="mt-2">
            <CriteriaChips
              criteria={criteria}
              onRemove={onRemoveCriterion}
              metricNames={metricNames}
            />
          </div>
        </section>
      )}

      {state.status === "idle" && <NoCriteriaState />}
      {state.status === "loading" && (
        <p role="status" className="mt-8 text-sm text-text-secondary">
          <fbt desc="Shown while the raw values table is loading">Chargement des valeurs…</fbt>
        </p>
      )}
      {state.status === "error" && <DataErrorState code={state.code} onReload={onReload} />}
      {data !== null && (
        <DataTableContent
          data={data}
          rows={orderedRows}
          sort={sort}
          locale={locale}
          stickyColumn={stickyColumn}
          containerRef={containerRef}
          hasMore={hasMore}
          onSortChange={onSortChange}
          onShowOnMap={onShowOnMap}
        />
      )}
    </div>
  );
}

function NoCriteriaState() {
  return (
    <p className="mt-8 rounded-2xl bg-surface p-6 text-sm text-text-secondary shadow-elevated">
      <fbt desc="Shown in the data view when no criterion is active">
        Aucun critère actif : ajoute un critère depuis le chat ou la carte pour afficher les valeurs
        brutes.
      </fbt>
    </p>
  );
}

function DataErrorState({ code, onReload }: { code: string; onReload: () => void }) {
  return (
    <div role="alert" className="mt-8 rounded-2xl bg-surface p-6 shadow-elevated">
      <p className="text-sm font-semibold">
        <fbt desc="Shown when the raw values cannot be loaded">
          Les valeurs n'ont pas pu être chargées.
        </fbt>
      </p>
      <p className="mt-1 text-xs text-text-secondary">
        <fbt desc="Label before the API error code">Code :</fbt> <span>{code}</span>
      </p>
      <Button variant="outline" size="sm" className="mt-4" onClick={onReload}>
        <fbt desc="Button retrying the raw values request">Réessayer</fbt>
      </Button>
    </div>
  );
}

function DataTableContent({
  data,
  rows,
  sort,
  locale,
  stickyColumn,
  containerRef,
  hasMore,
  onSortChange,
  onShowOnMap,
}: {
  data: DataTableData;
  rows: readonly DataTableRow[];
  sort: TableSort | null;
  locale: string;
  stickyColumn: string;
  containerRef: (element: HTMLElement | null) => void;
  hasMore: boolean;
  onSortChange: (sort: TableSort) => void;
  onShowOnMap: (neighborhoodId: number) => void;
}) {
  return (
    <>
      {data.unknownMetricKeys.length > 0 && (
        <p className="mt-6 rounded-xl border border-border bg-surface p-3 text-xs text-text-secondary">
          <fbt desc="Notice listing the active criteria that the API does not publish">
            Critères non publiés par l'API :
          </fbt>{" "}
          <span className="font-semibold text-text">{data.unknownMetricKeys.join(", ")}</span>
        </p>
      )}

      {data.metrics.length === 0 ? null : (
        <>
          <div className="relative mt-8 rounded-2xl shadow-elevated">
            <div ref={containerRef} className="relative overflow-x-auto rounded-2xl">
              <table className="w-full text-sm">
                <caption className="sr-only">
                  <fbt desc="Accessible caption of the raw values table">
                    Valeurs brutes par quartier : nom, district et un critère par colonne, triable.
                  </fbt>
                </caption>
                <thead>
                  <tr className="border-b border-border bg-surface text-left text-xs font-semibold text-text-secondary">
                    <th scope="col" className={cn(stickyColumn, "border-border px-4 py-3")}>
                      <fbt desc="Column header of the neighborhood name">Quartier</fbt>
                    </th>
                    <th scope="col" className="hidden px-4 py-3 sm:table-cell">
                      <fbt desc="Column header of the district name">District</fbt>
                    </th>
                    {data.metrics.map((metric) => {
                      const direction = sort?.key === metric.key ? sort.direction : null;
                      return (
                        <th
                          key={metric.key}
                          scope="col"
                          aria-sort={
                            direction === "ascending"
                              ? "ascending"
                              : direction === "descending"
                                ? "descending"
                                : "none"
                          }
                          className="p-0 text-right"
                        >
                          <button
                            type="button"
                            onClick={() => onSortChange(nextSort(sort, metric.key))}
                            className="flex min-h-11 w-full cursor-pointer flex-col items-end justify-end gap-0.5 px-4 py-3 text-right font-semibold transition-colors duration-150 ease-out hover:text-text focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-action sm:min-h-0"
                          >
                            <span className="flex items-center gap-1 whitespace-nowrap">
                              {localizedMetricName(metric, locale)}
                              <SortArrow direction={direction} />
                            </span>
                            <span className="text-[11px] font-normal whitespace-nowrap text-text-secondary">
                              <span className="sr-only">
                                <fbt desc="Accessible label of the unit and observation date in a column header">
                                  Unité et date d'observation :
                                </fbt>{" "}
                              </span>
                              <bdi dir="ltr">
                                {localizedUnit(metric.unit)} · {metric.observedAt ?? "—"}
                              </bdi>
                            </span>
                          </button>
                        </th>
                      );
                    })}
                    <th scope="col" className="px-4 py-3">
                      <span className="sr-only">
                        <fbt desc="Accessible column header of the map action">Carte</fbt>
                      </span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.neighborhoodId} className="even:bg-bg odd:bg-surface">
                      <th
                        scope="row"
                        className={cn(
                          stickyColumn,
                          "border-border px-4 py-3 text-left font-semibold",
                        )}
                      >
                        {row.name}
                        <span className="block text-xs font-normal text-text-secondary sm:hidden">
                          {row.districtName}
                        </span>
                      </th>
                      <td className="hidden px-4 py-3 text-text-secondary sm:table-cell">
                        {row.districtName}
                      </td>
                      {data.metrics.map((metric) => {
                        const value = row.values[metric.key];
                        return (
                          <td
                            key={metric.key}
                            className="px-4 py-3 text-right align-top whitespace-nowrap"
                          >
                            {value === undefined ? (
                              <>
                                <span aria-hidden="true" className="text-text-secondary">
                                  —
                                </span>
                                <span className="sr-only">
                                  <fbt desc="Shown when a neighborhood has no value for a metric">
                                    Pas de donnée
                                  </fbt>
                                </span>
                              </>
                            ) : (
                              <bdi dir="ltr">
                                <span className="tabular-nums">
                                  {formatMetricValue({
                                    value: value.raw,
                                    locale,
                                    maximumFractionDigits: metric.displayPrecision,
                                  })}
                                </span>{" "}
                                <span className="text-xs text-text-secondary">
                                  {localizedUnit(metric.unit)}
                                </span>
                                {value.qualityFlag === "ok" ? null : (
                                  <span className="block text-[10px] text-text-secondary">
                                    {metricQualityLabel(value.qualityFlag)}
                                  </span>
                                )}
                              </bdi>
                            )}
                          </td>
                        );
                      })}
                      <td className="px-4 py-3 text-right">
                        <Button
                          variant="outline"
                          size="sm"
                          className="whitespace-nowrap"
                          onClick={() => onShowOnMap(row.neighborhoodId)}
                        >
                          <fbt desc="Button opening the map view from a data row">
                            Voir sur la carte
                          </fbt>
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {hasMore && (
              <div
                aria-hidden="true"
                className="pointer-events-none absolute inset-y-0 end-0 z-20 w-10 rounded-e-2xl scroll-fade-inline-end"
              />
            )}
          </div>

          <ul className="mt-4 flex flex-wrap gap-x-4 gap-y-1 text-xs text-text-secondary">
            {data.metrics.map((metric) => (
              <li key={metric.key}>
                {localizedMetricName(metric, locale)} · {metric.source.organization} ·{" "}
                {shortLicense(metric.source.license)} · {metric.observedAt ?? "—"}
              </li>
            ))}
          </ul>

          <details className="mt-4 rounded-2xl bg-surface p-4 text-xs text-text-secondary shadow-elevated">
            <summary className="cursor-pointer font-semibold text-text">
              <fbt desc="Summary of the sources and dates panel under the data table">
                Sources et dates
              </fbt>
            </summary>
            <ul className="mt-3 flex flex-col gap-4">
              {data.metrics.map((metric) => (
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
      )}
    </>
  );
}

function SortArrow({ direction }: { direction: TableSort["direction"] | null }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        "inline-flex shrink-0 transition-[opacity,transform] duration-150 ease-out motion-reduce:transition-none",
        direction ? "opacity-100" : "opacity-0",
        direction === "descending" && "rotate-180",
      )}
    >
      <svg
        viewBox="0 0 12 12"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="size-3"
      >
        <path d="M6 9.5V2.5" />
        <path d="M3 5.5 6 2.5l3 3" />
      </svg>
    </span>
  );
}

function orderRows({
  data,
  sort,
  locale,
}: {
  data: DataTableData;
  sort: TableSort | null;
  locale: string;
}): DataTableRow[] {
  const byName = [...data.rows].sort((left, right) => left.name.localeCompare(right.name, locale));
  const activeSort =
    sort !== null && data.metrics.some((metric) => metric.key === sort.key) ? sort : null;
  return sortRows({
    rows: byName,
    sort: activeSort,
    sortValueOf: (row, key) => row.values[key]?.raw ?? null,
  });
}
