import { useMemo, useState } from "react";
import { fbs, fbt } from "fbtee";
import { ArrowUpDown, ChevronDown, ChevronLeft, ChevronRight, ChevronUp } from "lucide-react";
import {
  columnFilteringFeature,
  createColumnHelper,
  createFilteredRowModel,
  createPaginatedRowModel,
  createSortedRowModel,
  filterFn_includesString,
  globalFilteringFeature,
  rowPaginationFeature,
  rowSortingFeature,
  tableFeatures,
  useTable,
  type PaginationState,
  type SortingState,
} from "@tanstack/react-table";
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
import type { MetricDefinition } from "../metrics/metric-api-schema";
import type { TableSort } from "./table-sort";
import type { DataTableData, DataTableRow, DataTableValue } from "./data-table-client";
import type { DataTableState } from "./use-data-table";

const PAGE_SIZE = 50;

const features = tableFeatures({
  rowSortingFeature,
  columnFilteringFeature,
  globalFilteringFeature,
  rowPaginationFeature,
  sortedRowModel: createSortedRowModel(),
  filteredRowModel: createFilteredRowModel(),
  paginatedRowModel: createPaginatedRowModel(),
  filterFns: { includesString: filterFn_includesString },
});

const columnHelper = createColumnHelper<typeof features, DataTableRow>();

interface DataTableViewProps {
  criteria: Criteria;
  state: DataTableState;
  sort: TableSort | null;
  locale: string;
  onRemoveCriterion: (metricKey: string) => void;
  onSortChange: (sort: TableSort | null) => void;
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
  const unknownMetricKeys = useMemo(() => {
    const publishedKeys = new Set((data?.metrics ?? []).map((metric) => metric.key));
    return criteria.weights
      .map((criterion) => criterion.metricKey)
      .filter((metricKey) => !publishedKeys.has(metricKey));
  }, [criteria, data]);
  return (
    <div className="mx-auto w-full max-w-7xl flex-1 px-4 py-12 sm:px-8">
      <h1 className="text-4xl font-semibold tracking-tight text-balance">
        {sort === null ? (
          <fbt desc="Title of the data view when the rows are in alphabetical order">
            Valeurs par quartier
          </fbt>
        ) : (
          <fbt desc="Title of the data view when the rows are ranked by a metric">
            Classement des quartiers
          </fbt>
        )}
      </h1>
      <p className="mt-2 max-w-2xl text-sm text-text-secondary">
        <fbt desc="Subtitle of the data view, promising every published raw value with its unit, date and source">
          Toutes les métriques publiées, avec unité, date d'observation et source, sans score
          composite
        </fbt>
      </p>

      {criteria.weights.length > 0 && (
        <section className="mt-8" aria-labelledby="data-criteria-label">
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

      {state.status === "loading" && (
        <p role="status" className="mt-8 text-sm text-text-secondary">
          <fbt desc="Shown while the raw values table is loading">Chargement des valeurs…</fbt>
        </p>
      )}
      {state.status === "error" && <DataErrorState code={state.code} onReload={onReload} />}
      {data !== null && (
        <DataTableContent
          data={data}
          sort={sort}
          locale={locale}
          unknownMetricKeys={unknownMetricKeys}
          overflows={overflows}
          containerRef={containerRef}
          hasMore={hasMore}
          onSortChange={onSortChange}
          onShowOnMap={onShowOnMap}
        />
      )}
    </div>
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
  sort,
  locale,
  unknownMetricKeys,
  overflows,
  containerRef,
  hasMore,
  onSortChange,
  onShowOnMap,
}: {
  data: DataTableData;
  sort: TableSort | null;
  locale: string;
  unknownMetricKeys: readonly string[];
  overflows: boolean;
  containerRef: (element: HTMLElement | null) => void;
  hasMore: boolean;
  onSortChange: (sort: TableSort | null) => void;
  onShowOnMap: (neighborhoodId: number) => void;
}) {
  const rows = useMemo(
    () => [...data.rows].sort((left, right) => left.name.localeCompare(right.name, locale)),
    [data, locale],
  );
  const metricByKey = useMemo(
    () => new Map(data.metrics.map((metric) => [metric.key, metric])),
    [data.metrics],
  );
  const sortedMetric = sort === null ? undefined : metricByKey.get(sort.key);
  const highlightsMerit =
    sort !== null &&
    sortedMetric !== undefined &&
    ((sort.direction === "descending" && sortedMetric.polarity === "higher_is_better") ||
      (sort.direction === "ascending" && sortedMetric.polarity === "lower_is_better"));
  const columns = useMemo(
    () =>
      columnHelper.columns([
        columnHelper.accessor((row) => `${row.name} ${row.districtName}`, {
          id: "name",
          header: () => fbt("Quartier", "Column header of the neighborhood name"),
          enableSorting: false,
          cell: ({ row }) => (
            <span className="flex max-w-[9.5rem] items-baseline gap-2 whitespace-nowrap sm:max-w-none">
              <button
                type="button"
                title={row.original.name}
                onClick={() => onShowOnMap(row.original.neighborhoodId)}
                className="min-w-0 cursor-pointer truncate text-start font-medium text-text transition-colors duration-150 ease-out hover:text-action hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
              >
                {row.original.name}
              </button>
              <span className="hidden text-xs font-normal text-text-secondary sm:inline">
                {row.original.districtName}
              </span>
            </span>
          ),
        }),
        ...data.metrics.map((metric) =>
          columnHelper.accessor((row) => row.values[metric.key]?.raw, {
            id: metric.key,
            header: () => localizedMetricName(metric, locale),
            enableGlobalFilter: false,
            sortUndefined: "last" as const,
            cell: ({ row }) => (
              <MetricCell
                value={row.original.values[metric.key]}
                metric={metric}
                locale={locale}
                highlighted={sortedMetric?.key === metric.key}
              />
            ),
          }),
        ),
        columnHelper.display({
          id: "actions",
          header: () => (
            <span className="sr-only">
              <fbt desc="Accessible column header of the map action">Carte</fbt>
            </span>
          ),
          enableSorting: false,
          cell: ({ row }) => (
            <Button
              variant="outline"
              size="sm"
              className="whitespace-nowrap"
              onClick={() => onShowOnMap(row.original.neighborhoodId)}
            >
              <fbt desc="Button opening the map view from a data row">Voir sur la carte</fbt>
            </Button>
          ),
        }),
      ]),
    [data.metrics, locale, onShowOnMap, sortedMetric?.key],
  );
  const sorting = useMemo<SortingState>(
    () => (sort === null ? [] : [{ id: sort.key, desc: sort.direction === "descending" }]),
    [sort],
  );
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: PAGE_SIZE,
  });
  const table = useTable({
    features,
    columns,
    data: rows,
    getRowId: (row) => String(row.neighborhoodId),
    state: { sorting, pagination },
    onPaginationChange: setPagination,
    onSortingChange: (updater) => {
      const nextSorting = typeof updater === "function" ? updater(sorting) : updater;
      const first = nextSorting[0];
      if (first === undefined) {
        onSortChange(null);
        return;
      }
      onSortChange({
        key: first.id,
        direction: first.desc ? "descending" : "ascending",
      });
    },
    enableSortingRemoval: true,
    enableMultiSort: false,
    globalFilterFn: "includesString",
    getColumnCanGlobalFilter: (column) => column.id === "name",
  });
  const searchText = typeof table.state.globalFilter === "string" ? table.state.globalFilter : "";
  const pageRows = table.getRowModel().rows;
  const pageCount = table.getPageCount();
  const firstRank = pagination.pageIndex * pagination.pageSize;

  const pageResetKey = `${sort?.key ?? ""}|${sort?.direction ?? ""}|${searchText}`;
  const [previousPageResetKey, setPreviousPageResetKey] = useState(pageResetKey);
  if (previousPageResetKey !== pageResetKey) {
    setPreviousPageResetKey(pageResetKey);
    setPagination((current) => (current.pageIndex === 0 ? current : { ...current, pageIndex: 0 }));
  }

  return (
    <>
      {unknownMetricKeys.length > 0 && (
        <p className="mt-6 rounded-xl border border-border bg-surface p-3 text-xs text-text-secondary">
          <fbt desc="Notice listing the active criteria that the API does not publish">
            Critères non publiés par l'API :
          </fbt>{" "}
          <span className="font-semibold text-text">{unknownMetricKeys.join(", ")}</span>
        </p>
      )}

      <div className="mt-8 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <label className="flex w-full max-w-sm items-center gap-2 border-b border-border text-sm focus-within:border-action">
          <span className="sr-only">
            <fbt desc="Label of the search field filtering the raw values table">
              Rechercher un quartier
            </fbt>
          </span>
          <input
            type="search"
            value={searchText}
            onChange={(event) => table.setGlobalFilter(event.target.value)}
            placeholder={fbs(
              "Nom du quartier ou du district",
              "Placeholder of the raw values table search field",
            )}
            className="h-9 w-full bg-transparent px-0 text-sm text-text placeholder:text-text-secondary focus-visible:outline-none"
          />
        </label>
        <p role="status" className="text-xs text-text-secondary">
          <fbt desc="Count of neighborhoods currently displayed in the raw values table">
            Quartiers affichés :
          </fbt>{" "}
          <span className="font-mono tabular-nums">
            {pageRows.length} / {data.rows.length}
          </span>
        </p>
      </div>

      <div className="relative mt-3">
        <div ref={containerRef} className="relative overflow-x-auto">
          <table className="w-full border-collapse text-sm">
            <caption className="sr-only">
              <fbt desc="Accessible caption of the raw values table">
                Valeurs brutes par quartier : nom, district et une colonne par métrique publiée,
                triable.
              </fbt>
            </caption>
            <thead>
              <tr className="border-b border-border">
                <th
                  scope="col"
                  className={cn(
                    "sticky start-0 z-10 w-12 bg-bg px-2 py-2 text-center align-middle text-sm font-medium text-text-secondary",
                    overflows && "border-e",
                  )}
                >
                  <fbt desc="Header of the ranking column in the data table">#</fbt>
                </th>
                {table.getHeaderGroups()[0]?.headers.map((header) => {
                  const column = header.column;
                  const isName = column.id === "name";
                  const metric = metricByKey.get(column.id);
                  const sorted = column.getIsSorted();
                  const ariaSort: "ascending" | "descending" | "none" =
                    sorted === "asc" ? "ascending" : sorted === "desc" ? "descending" : "none";
                  const highlighted = sortedMetric?.key === column.id;

                  if (isName) {
                    return (
                      <th
                        key={header.id}
                        scope="col"
                        className="sticky start-12 z-10 border-e border-border bg-bg px-2 py-2 text-left align-middle text-sm font-medium text-text"
                      >
                        <table.FlexRender header={header} />
                      </th>
                    );
                  }

                  if (metric === undefined) {
                    return (
                      <th
                        key={header.id}
                        scope="col"
                        className="px-2 py-2 text-right align-middle text-sm font-medium text-text-secondary"
                      >
                        <table.FlexRender header={header} />
                      </th>
                    );
                  }

                  return (
                    <th
                      key={header.id}
                      scope="col"
                      aria-sort={ariaSort}
                      className={cn(
                        "px-2 align-middle whitespace-nowrap",
                        highlighted && "bg-highlight/10",
                      )}
                    >
                      <button
                        type="button"
                        onClick={column.getToggleSortingHandler()}
                        className={cn(
                          "flex min-h-11 w-full cursor-pointer flex-col items-end justify-end gap-0.5 px-1 py-1.5 text-right transition-colors duration-150 ease-out focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-action sm:min-h-0",
                          highlighted
                            ? "font-semibold text-text"
                            : "font-medium text-text-secondary hover:text-text",
                        )}
                      >
                        <span className="flex items-center gap-1">
                          <table.FlexRender header={header} />
                          <SortIcon
                            direction={sorted === "asc" || sorted === "desc" ? sorted : null}
                          />
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
              </tr>
            </thead>
            <tbody>
              {pageRows.map((row, index) => (
                <tr
                  key={row.id}
                  className="group border-b border-border transition-colors duration-150 ease-out hover:bg-surface"
                >
                  <td
                    className={cn(
                      "sticky start-0 z-10 w-12 bg-bg px-2 py-2 text-center align-middle group-hover:bg-surface",
                      overflows && "border-e",
                    )}
                  >
                    <RankBadge rank={firstRank + index + 1} highlightMerit={highlightsMerit} />
                  </td>
                  {row.getAllCells().map((cell) => {
                    const isName = cell.column.id === "name";
                    const isMetric = metricByKey.has(cell.column.id);
                    const content = <table.FlexRender cell={cell} />;

                    if (isName) {
                      return (
                        <th
                          key={cell.id}
                          scope="row"
                          className="sticky start-12 z-10 border-e border-border bg-bg px-2 py-2 text-left align-middle text-sm font-medium group-hover:bg-surface"
                        >
                          {content}
                        </th>
                      );
                    }

                    return (
                      <td
                        key={cell.id}
                        className={cn(
                          "px-2 py-2 text-right align-middle whitespace-nowrap",
                          isMetric && sortedMetric?.key === cell.column.id && "bg-highlight/10",
                        )}
                      >
                        {content}
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {hasMore && (
          <div
            aria-hidden="true"
            className="pointer-events-none absolute inset-y-0 end-0 z-20 w-10 scroll-fade-inline-end"
          />
        )}
      </div>

      {pageRows.length === 0 && (
        <p role="status" className="mt-4 text-sm text-text-secondary">
          <fbt desc="Shown when the neighborhood search matches nothing in the raw values table">
            Aucun quartier ne correspond à cette recherche.
          </fbt>
        </p>
      )}

      {pageCount > 1 && (
        <nav
          aria-label={fbs(
            "Pagination du tableau des quartiers",
            "Accessible label of the data table pagination",
          )}
          className="mt-4 flex items-center justify-between gap-3 border-t border-border pt-4"
        >
          <p className="text-xs text-text-secondary">
            <fbt desc="Label of the current page in the data table pagination">Page</fbt>{" "}
            <span className="font-mono tabular-nums">{pagination.pageIndex + 1}</span>
            <span> / </span>
            <span className="font-mono tabular-nums">{pageCount}</span>
          </p>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              disabled={!table.getCanPreviousPage()}
              onClick={() => table.previousPage()}
            >
              <ChevronLeft aria-hidden="true" className="size-4 rtl:rotate-180" />
              <fbt desc="Button showing the previous page of the data table">Précédent</fbt>
            </Button>
            <Button
              variant="ghost"
              size="sm"
              disabled={!table.getCanNextPage()}
              onClick={() => table.nextPage()}
            >
              <fbt desc="Button showing the next page of the data table">Suivant</fbt>
              <ChevronRight aria-hidden="true" className="size-4 rtl:rotate-180" />
            </Button>
          </div>
        </nav>
      )}

      <ul className="mt-6 flex flex-wrap gap-x-4 gap-y-1 text-xs text-text-secondary">
        {data.metrics.map((metric) => (
          <li key={metric.key}>
            {localizedMetricName(metric, locale)} · {metric.source.organization} ·{" "}
            {shortLicense(metric.source.license)} · {metric.observedAt ?? "—"}
          </li>
        ))}
      </ul>

      <details className="mt-6 border-t border-border pt-4 text-xs text-text-secondary">
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
  );
}

function MetricCell({
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

function RankBadge({ rank, highlightMerit }: { rank: number; highlightMerit: boolean }) {
  if (!highlightMerit || rank > 3) {
    return (
      <span data-rank={rank} className="font-mono text-xs text-text-secondary tabular-nums">
        {rank}
      </span>
    );
  }

  return (
    <span
      data-rank={rank}
      data-merit="true"
      className={cn(
        "inline-flex size-6 items-center justify-center rounded-full font-mono text-xs font-semibold tabular-nums",
        rank === 1 && "bg-action text-action-text",
        rank === 2 && "bg-highlight text-highlight-text",
        rank === 3 && "bg-surface text-text ring-1 ring-border ring-inset",
      )}
    >
      {rank}
    </span>
  );
}

function SortIcon({ direction }: { direction: "asc" | "desc" | null }) {
  if (direction === "asc") {
    return <ChevronUp aria-hidden="true" className="size-3.5 shrink-0 text-action" />;
  }
  if (direction === "desc") {
    return <ChevronDown aria-hidden="true" className="size-3.5 shrink-0 text-action" />;
  }
  return <ArrowUpDown aria-hidden="true" className="size-3.5 shrink-0 opacity-20" />;
}
