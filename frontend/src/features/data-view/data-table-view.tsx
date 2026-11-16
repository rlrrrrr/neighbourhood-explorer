import { ViewTransition, startTransition, useState } from "react";
import { useTable, type PaginationState, type SortingState } from "@tanstack/react-table";
import { ArrowUpDown, ChevronDown, ChevronUp } from "lucide-react";
import { Button } from "../../components/ui/button";
import { cn } from "../../components/ui/cn";
import { useScrollOverflowHint } from "../../components/ui/scroll-affordance";
import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
  TableRowHeader,
} from "../../components/ui/table";
import { CriteriaChips } from "../criteria/criteria-chips";
import type { Criteria } from "../criteria/criteria";
import { localizedMetricName, localizedUnit } from "../metrics/metric-presentation";
import type { TableSort } from "./table-sort";
import { createDataTableColumns, dataTableFeatures } from "./data-table-columns";
import { DataTablePagination } from "./data-table-pagination";
import { DataTableSources } from "./data-table-sources";
import { DataTableToolbar } from "./data-table-toolbar";
import type { DataTableData } from "./data-table-client";
import type { DataTableState } from "./use-data-table";

const PAGE_SIZE = 50;

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

  const metricNames = new Map(
    (data?.metrics ?? []).map((metric) => [metric.key, localizedMetricName(metric, locale)]),
  );

  const publishedKeys = new Set((data?.metrics ?? []).map((metric) => metric.key));

  const unknownMetricKeys = criteria.weights
    .map((criterion) => criterion.metricKey)
    .filter((metricKey) => !publishedKeys.has(metricKey));

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
  const rows = [...data.rows].sort((left, right) => left.name.localeCompare(right.name, locale));
  const metricByKey = new Map(data.metrics.map((metric) => [metric.key, metric]));
  const sortedMetric = sort === null ? undefined : metricByKey.get(sort.key);

  const highlightsMerit =
    sort !== null &&
    sortedMetric !== undefined &&
    ((sort.direction === "descending" && sortedMetric.polarity === "higher_is_better") ||
      (sort.direction === "ascending" && sortedMetric.polarity === "lower_is_better"));

  const columns = createDataTableColumns({
    data,
    locale,
    sortedMetricKey: sortedMetric?.key,
    onShowOnMap,
  });

  const sorting: SortingState =
    sort === null ? [] : [{ id: sort.key, desc: sort.direction === "descending" }];

  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: PAGE_SIZE,
  });

  const table = useTable({
    features: dataTableFeatures,
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

      <DataTableToolbar
        searchText={searchText}
        shownCount={pageRows.length}
        totalCount={data.rows.length}
        onSearchChange={(value) => table.setGlobalFilter(value)}
      />

      <ViewTransition default="none" update="table-update">
        <div className="relative mt-3">
          <Table containerRef={containerRef}>
            <TableCaption className="sr-only">
              <fbt desc="Accessible caption of the raw values table">
                Valeurs brutes par quartier : nom, district et une colonne par métrique publiée,
                triable.
              </fbt>
            </TableCaption>
            <TableHeader>
              <TableRow>
                <TableHead
                  scope="col"
                  className={cn(
                    "sticky start-0 z-10 w-12 bg-bg text-center text-sm text-text-secondary",
                    overflows && "border-e",
                  )}
                >
                  <fbt desc="Header of the ranking column in the data table">#</fbt>
                </TableHead>
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
                      <TableHead
                        key={header.id}
                        scope="col"
                        className="sticky start-12 z-10 border-e border-border bg-bg text-start text-sm text-text"
                      >
                        <table.FlexRender header={header} />
                      </TableHead>
                    );
                  }

                  if (metric === undefined) {
                    return (
                      <TableHead
                        key={header.id}
                        scope="col"
                        className="text-end text-sm text-text-secondary"
                      >
                        <table.FlexRender header={header} />
                      </TableHead>
                    );
                  }

                  return (
                    <TableHead
                      key={header.id}
                      scope="col"
                      aria-sort={ariaSort}
                      className={cn(highlighted && "bg-highlight/10")}
                    >
                      <button
                        type="button"
                        onClick={column.getToggleSortingHandler()}
                        className={cn(
                          "flex min-h-11 w-full cursor-pointer flex-col items-end justify-end gap-0.5 px-1 py-1.5 text-end transition-colors duration-150 ease-out focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-action sm:min-h-0",
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
                    </TableHead>
                  );
                })}
              </TableRow>
            </TableHeader>
            <TableBody>
              {pageRows.map((row, index) => (
                <TableRow key={row.id} className="group hover:bg-surface">
                  <TableCell
                    className={cn(
                      "sticky start-0 z-10 w-12 bg-bg text-center group-hover:bg-surface",
                      overflows && "border-e",
                    )}
                  >
                    <RankBadge rank={firstRank + index + 1} highlightMerit={highlightsMerit} />
                  </TableCell>
                  {row.getAllCells().map((cell) => {
                    const isName = cell.column.id === "name";
                    const isMetric = metricByKey.has(cell.column.id);
                    const content = <table.FlexRender cell={cell} />;

                    if (isName) {
                      return (
                        <TableRowHeader
                          key={cell.id}
                          className="sticky start-12 z-10 border-e border-border bg-bg text-sm group-hover:bg-surface"
                        >
                          {content}
                        </TableRowHeader>
                      );
                    }

                    return (
                      <TableCell
                        key={cell.id}
                        className={cn(
                          "text-end",
                          isMetric && sortedMetric?.key === cell.column.id && "bg-highlight/10",
                        )}
                      >
                        {content}
                      </TableCell>
                    );
                  })}
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {hasMore && (
            <div
              aria-hidden="true"
              className="pointer-events-none absolute inset-y-0 end-0 z-20 w-10 scroll-fade-inline-end"
            />
          )}
        </div>
      </ViewTransition>

      {pageRows.length === 0 && (
        <p role="status" className="mt-4 text-sm text-text-secondary">
          <fbt desc="Shown when the neighborhood search matches nothing in the raw values table">
            Aucun quartier ne correspond à cette recherche.
          </fbt>
        </p>
      )}

      {pageCount > 1 && (
        <DataTablePagination
          pageIndex={pagination.pageIndex}
          pageCount={pageCount}
          canPreviousPage={table.getCanPreviousPage()}
          canNextPage={table.getCanNextPage()}
          onPreviousPage={() => startTransition(() => table.previousPage())}
          onNextPage={() => startTransition(() => table.nextPage())}
        />
      )}

      <DataTableSources metrics={data.metrics} locale={locale} />
    </>
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
