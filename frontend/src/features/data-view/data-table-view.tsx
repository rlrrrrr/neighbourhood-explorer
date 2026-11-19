import { Fragment, ViewTransition, startTransition, useState } from "react";
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
import {
  formatMetricValue,
  localizedMetricName,
  localizedUnit,
  metricQualityLabel,
} from "../metrics/metric-presentation";
import {
  districtGroups,
  orderGroupedRows,
  visibleMetricKeys,
  type DistrictGroup,
} from "./data-table-grouping";
import { dominantQualityFlags } from "./data-table-quality";
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
  const { containerRef, hasMore } = useScrollOverflowHint({ axis: "horizontal" });
  const data = state.status === "ready" ? state.data : null;

  const metricNames = new Map(
    (data?.metrics ?? []).map((metric) => [metric.key, localizedMetricName(metric, locale)]),
  );

  const publishedKeys = new Set((data?.metrics ?? []).map((metric) => metric.key));

  const unknownMetricKeys = criteria.weights
    .map((criterion) => criterion.metricKey)
    .filter((metricKey) => !publishedKeys.has(metricKey));

  return (
    <div className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-8">
      <h1 className="text-2xl font-semibold tracking-tight text-balance">
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
      <p className="mt-1.5 max-w-2xl text-sm text-text-secondary">
        <fbt desc="Subtitle of the data view explaining that it compares one raw metric and that the neighborhood sheet on the map holds every value">
          Compare une métrique brute à la fois, sans score composite. La fiche d'un quartier sur la
          carte rassemble toutes ses valeurs.
        </fbt>
      </p>

      {criteria.weights.length > 0 && (
        <section className="mt-6" aria-labelledby="data-criteria-label">
          <h2 id="data-criteria-label" className="text-[11px] font-semibold text-text-secondary">
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
  containerRef,
  hasMore,
  onSortChange,
  onShowOnMap,
}: {
  data: DataTableData;
  sort: TableSort | null;
  locale: string;
  unknownMetricKeys: readonly string[];
  containerRef: (element: HTMLElement | null) => void;
  hasMore: boolean;
  onSortChange: (sort: TableSort | null) => void;
  onShowOnMap: (neighborhoodId: number) => void;
}) {
  const [groupByDistrict, setGroupByDistrict] = useState(true);
  const [showGroupAverage, setShowGroupAverage] = useState(true);
  const [collapsedDistricts, setCollapsedDistricts] = useState<readonly string[]>([]);

  const rows = groupByDistrict
    ? orderGroupedRows({ rows: data.rows, sort, locale })
    : [...data.rows].sort((left, right) => left.name.localeCompare(right.name, locale));

  const metricByKey = new Map(data.metrics.map((metric) => [metric.key, metric]));
  const sortedMetric = sort === null ? undefined : metricByKey.get(sort.key);

  const highlightsMerit =
    sort !== null &&
    sortedMetric !== undefined &&
    ((sort.direction === "descending" && sortedMetric.polarity === "higher_is_better") ||
      (sort.direction === "ascending" && sortedMetric.polarity === "lower_is_better"));

  const visibleKeys = visibleMetricKeys({
    metrics: data.metrics,
    sortedMetricKey: sortedMetric?.key,
  });

  const qualityFlags = dominantQualityFlags({ rows: data.rows, metricKeys: visibleKeys });

  const groupMetric = metricByKey.get(visibleKeys[0] ?? "");

  const columns = createDataTableColumns({
    data,
    locale,
    visibleMetricKeys: visibleKeys,
    sortedMetricKey: sortedMetric?.key,
    dominantQualityFlags: qualityFlags,
    highlightsMerit,
    showDistrict: !groupByDistrict,
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
    manualSorting: groupByDistrict,
    state: { sorting, pagination },
    autoResetPageIndex: false,
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

  type TableRow = (typeof pageRows)[number];

  const groupsByDistrict = new Map(
    districtGroups({
      rows: table.getFilteredRowModel().rows.map((row) => row.original),
      metricKey: showGroupAverage ? groupMetric?.key : undefined,
    }).map((group) => [group.districtName, group]),
  );

  type Section = {
    districtName: string | null;
    group: DistrictGroup | null;
    collapsed: boolean;
    rows: Array<{ row: TableRow; rank: number }>;
  };

  const sections: Section[] = [];

  if (groupByDistrict) {
    for (const [index, row] of pageRows.entries()) {
      const districtName = row.original.districtName;
      const previousSection = sections.at(-1);

      const section =
        previousSection?.districtName === districtName
          ? previousSection
          : {
              districtName,
              group: groupsByDistrict.get(districtName) ?? null,
              collapsed: collapsedDistricts.includes(districtName),
              rows: [],
            };

      if (section !== previousSection) {
        sections.push(section);
      }

      section.rows.push({ row, rank: firstRank + index + 1 });
    }
  } else {
    sections.push({
      districtName: null,
      group: null,
      collapsed: false,
      rows: pageRows.map((row, index) => ({ row, rank: firstRank + index + 1 })),
    });
  }

  const shownCount = sections.reduce(
    (count, section) => count + (section.collapsed ? 0 : section.rows.length),
    0,
  );

  const toggleDistrict = (districtName: string) => {
    setCollapsedDistricts((current) =>
      current.includes(districtName)
        ? current.filter((name) => name !== districtName)
        : [...current, districtName],
    );
  };

  const pageResetKey = `${sort?.key ?? ""}|${sort?.direction ?? ""}|${searchText}|${groupByDistrict}`;
  const [previousPageResetKey, setPreviousPageResetKey] = useState(pageResetKey);

  if (previousPageResetKey !== pageResetKey) {
    setPreviousPageResetKey(pageResetKey);
    setPagination((current) => (current.pageIndex === 0 ? current : { ...current, pageIndex: 0 }));
  }

  return (
    <>
      {unknownMetricKeys.length > 0 && (
        <p className="mt-5 rounded-xl border border-border/50 bg-surface/60 p-3 text-xs text-text-secondary">
          <fbt desc="Notice listing the active criteria that the API does not publish">
            Critères non publiés par l'API :
          </fbt>{" "}
          <span className="font-semibold text-text">{unknownMetricKeys.join(", ")}</span>
        </p>
      )}

      <DataTableToolbar
        searchText={searchText}
        shownCount={shownCount}
        totalCount={data.rows.length}
        metrics={data.metrics}
        locale={locale}
        sort={sort}
        groupByDistrict={groupByDistrict}
        showGroupAverage={showGroupAverage}
        onToggleGrouping={() => setGroupByDistrict((current) => !current)}
        onToggleAverage={() => setShowGroupAverage((current) => !current)}
        onSearchChange={(value) => table.setGlobalFilter(value)}
        onSortChange={onSortChange}
      />

      <ViewTransition default="none" update="table-update">
        <div className="relative mt-3">
          <Table
            containerRef={containerRef}
            containerClassName="rounded-xl border border-border/40"
          >
            <TableCaption className="sr-only">
              <fbt desc="Accessible caption of the raw values table">
                Valeurs brutes par quartier, regroupées par district et triables par métrique.
              </fbt>
            </TableCaption>
            <TableHeader>
              <TableRow>
                <TableHead
                  scope="col"
                  className="sticky start-0 z-10 w-9 bg-bg text-center text-[10px] font-medium text-text-secondary"
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
                  const qualityFlag = qualityFlags.get(column.id);

                  if (isName) {
                    return (
                      <TableHead
                        key={header.id}
                        scope="col"
                        className="sticky start-9 z-10 w-[30%] bg-bg text-start text-[11px] font-medium text-text sm:w-auto"
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
                        className="w-10 text-end text-[11px] font-medium text-text-secondary"
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
                      className={cn("group/head w-px text-end", highlighted && "bg-action/[0.04]")}
                    >
                      <button
                        type="button"
                        onClick={column.getToggleSortingHandler()}
                        className="flex w-full cursor-pointer flex-col items-end gap-0.5 rounded py-1 text-end transition-colors duration-150 ease-out hover:bg-surface/60 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-action"
                      >
                        <span
                          className={cn(
                            "flex items-center gap-1 whitespace-normal sm:whitespace-nowrap",
                            highlighted
                              ? "text-[11px] font-semibold text-text"
                              : "text-[11px] font-medium text-text-secondary",
                          )}
                        >
                          <table.FlexRender header={header} />
                          <span
                            className={cn(
                              "inline-flex transition-opacity duration-150 ease-out",
                              sorted === false &&
                                "opacity-0 group-hover/head:opacity-100 group-focus-within/head:opacity-100",
                            )}
                          >
                            <SortIcon
                              direction={sorted === "asc" || sorted === "desc" ? sorted : null}
                            />
                          </span>
                        </span>
                        <span className="flex items-center gap-1 whitespace-nowrap text-[10px] font-normal text-text-secondary">
                          <span className="hidden items-center gap-1 sm:inline-flex">
                            <span className="sr-only">
                              <fbt desc="Accessible label of the unit and observation date in a column header">
                                Unité et date d'observation :
                              </fbt>{" "}
                            </span>
                            <bdi dir="ltr">
                              {localizedUnit(metric.unit)} · {metric.observedAt ?? "—"}
                            </bdi>
                          </span>
                          {qualityFlag !== undefined && qualityFlag !== "ok" && (
                            <span>
                              <span className="hidden sm:inline">· </span>
                              <span className="sr-only">
                                <fbt desc="Accessible label of the metric quality in a column header">
                                  Qualité :
                                </fbt>{" "}
                              </span>
                              {metricQualityLabel(qualityFlag)}
                            </span>
                          )}
                        </span>
                      </button>
                    </TableHead>
                  );
                })}
              </TableRow>
            </TableHeader>
            {sections.map((section, sectionIndex) => {
              const rowsId = `district-rows-${sectionIndex}`;
              const group = section.group;

              const averageText =
                showGroupAverage &&
                group?.average !== null &&
                group?.average !== undefined &&
                groupMetric !== undefined
                  ? formatMetricValue({
                      value: group.average,
                      locale,
                      maximumFractionDigits: groupMetric.displayPrecision,
                    })
                  : null;

              const barWidth =
                group !== null && group.average !== null && group.maximum > 0
                  ? Math.max(0, Math.min(100, (group.average / group.maximum) * 100))
                  : 0;

              return (
                <Fragment key={`${section.districtName ?? "all"}-${sectionIndex}`}>
                  {group !== null && (
                    <TableBody>
                      <TableRow className="border-t border-border/40 bg-surface hover:bg-action/[0.035]">
                        <TableRowHeader
                          scope="rowgroup"
                          colSpan={visibleKeys.length + 3}
                          className="p-0"
                        >
                          <button
                            type="button"
                            aria-expanded={!section.collapsed}
                            aria-controls={rowsId}
                            onClick={() => toggleDistrict(group.districtName)}
                            className="flex w-full cursor-pointer items-center gap-2 px-3 py-1.5 text-start focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-action"
                          >
                            <span className="sr-only">
                              <fbt desc="Accessible label of the button collapsing a district group">
                                Afficher ou masquer les quartiers du district
                              </fbt>{" "}
                            </span>
                            <ChevronDown
                              aria-hidden="true"
                              className={cn(
                                "size-3.5 shrink-0 text-text-secondary transition-transform duration-150 ease-out",
                                section.collapsed && "-rotate-90 rtl:rotate-90",
                              )}
                            />
                            <span
                              aria-hidden="true"
                              className="size-1.5 shrink-0 rounded-full bg-info"
                            />
                            <span className="text-xs font-semibold text-text">
                              {group.districtName}
                            </span>
                            <span className="text-[11px] text-text-secondary tabular-nums">
                              {group.count}
                            </span>
                            {averageText !== null && (
                              <span className="ms-auto flex items-center gap-2 text-[11px] font-normal text-text-secondary">
                                <span>
                                  <fbt desc="Label of the district average in the data table">
                                    moy.
                                  </fbt>{" "}
                                  <bdi dir="ltr">{averageText}</bdi>
                                </span>
                                <span
                                  aria-hidden="true"
                                  className="inline-block h-0.5 w-10 overflow-hidden rounded-full bg-border"
                                >
                                  <span
                                    className="block h-full rounded-full bg-action"
                                    style={{ width: `${barWidth}%` }}
                                  />
                                </span>
                              </span>
                            )}
                          </button>
                        </TableRowHeader>
                      </TableRow>
                    </TableBody>
                  )}
                  <TableBody id={rowsId}>
                    {section.collapsed
                      ? null
                      : section.rows.map(({ row, rank }) => (
                          <TableRow key={row.id} className="group hover:bg-surface">
                            <TableCell className="sticky start-0 z-10 w-9 bg-bg text-center group-hover:bg-surface">
                              <RankBadge rank={rank} highlightMerit={highlightsMerit} />
                            </TableCell>
                            {row.getAllCells().map((cell) => {
                              const isName = cell.column.id === "name";
                              const isMetric = metricByKey.has(cell.column.id);
                              const isActions = cell.column.id === "actions";
                              const content = <table.FlexRender cell={cell} />;

                              if (isName) {
                                return (
                                  <TableRowHeader
                                    key={cell.id}
                                    className="sticky start-9 z-10 w-[30%] bg-bg text-[13px] font-medium text-text group-hover:bg-surface sm:w-auto"
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
                                    isActions && "w-10 py-0",
                                    isMetric &&
                                      sortedMetric?.key === cell.column.id &&
                                      "bg-action/[0.04]",
                                  )}
                                >
                                  {content}
                                </TableCell>
                              );
                            })}
                          </TableRow>
                        ))}
                  </TableBody>
                </Fragment>
              );
            })}
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
          onFirstPage={() => startTransition(() => table.firstPage())}
        />
      )}

      <DataTableSources metrics={data.metrics} locale={locale} />
    </>
  );
}

function RankBadge({ rank, highlightMerit }: { rank: number; highlightMerit: boolean }) {
  if (!highlightMerit || rank > 3) {
    return (
      <span data-rank={rank} className="text-[11px] text-text-secondary tabular-nums">
        {rank}
      </span>
    );
  }

  return (
    <span
      data-rank={rank}
      data-merit="true"
      className={cn(
        "inline-flex size-5 items-center justify-center rounded text-[11px] font-semibold tabular-nums",
        rank === 1 && "bg-action text-action-text",
        rank === 2 && "bg-action/70 text-action-text",
        rank === 3 && "bg-action/10 text-text",
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

  return <ArrowUpDown aria-hidden="true" className="size-3.5 shrink-0 text-text-secondary" />;
}
