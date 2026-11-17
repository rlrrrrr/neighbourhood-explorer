import { fbt, fbs } from "fbtee";
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
} from "@tanstack/react-table";
import { ChevronDown } from "lucide-react";
import { cn } from "../../components/ui/cn";
import { localizedMetricName } from "../metrics/metric-presentation";
import type { DataTableData, DataTableRow } from "./data-table-client";
import { MetricCell } from "./metric-cell";

export const dataTableFeatures = tableFeatures({
  rowSortingFeature,
  columnFilteringFeature,
  globalFilteringFeature,
  rowPaginationFeature,
  sortedRowModel: createSortedRowModel(),
  filteredRowModel: createFilteredRowModel(),
  paginatedRowModel: createPaginatedRowModel(),
  filterFns: { includesString: filterFn_includesString },
});

const columnHelper = createColumnHelper<typeof dataTableFeatures, DataTableRow>();

export function createDataTableColumns({
  data,
  locale,
  visibleMetricKeys,
  sortedMetricKey,
  highlightsMerit,
  expandedNeighborhoodId,
  onToggleExpanded,
  onShowOnMap,
}: {
  data: DataTableData;
  locale: string;
  visibleMetricKeys: readonly string[];
  sortedMetricKey: string | undefined;
  highlightsMerit: boolean;
  expandedNeighborhoodId: number | null;
  onToggleExpanded: (neighborhoodId: number) => void;
  onShowOnMap: (neighborhoodId: number) => void;
}) {
  const visibleMetrics = data.metrics.filter((metric) => visibleMetricKeys.includes(metric.key));

  return columnHelper.columns([
    columnHelper.accessor((row) => `${row.name} ${row.districtName}`, {
      id: "name",
      header: () => fbt("Quartier", "Column header of the neighborhood name"),
      enableSorting: false,
      cell: ({ row }) => {
        const neighborhoodId = row.original.neighborhoodId;
        const expanded = expandedNeighborhoodId === neighborhoodId;

        return (
          <span className="flex max-w-[9.5rem] items-baseline gap-1.5 whitespace-nowrap sm:max-w-none">
            <button
              type="button"
              aria-expanded={expanded}
              aria-controls={`metric-detail-${neighborhoodId}`}
              aria-label={fbs(
                "Afficher ou masquer les détails du quartier",
                "Accessible label of the button expanding a neighborhood row",
              )}
              onClick={() => onToggleExpanded(neighborhoodId)}
              className="flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-md border border-border text-text-secondary transition-[transform,background-color] duration-150 ease-out hover:bg-surface active:scale-[0.97] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
            >
              <ChevronDown
                aria-hidden="true"
                className={cn(
                  "size-3.5 transition-transform duration-150 ease-out",
                  expanded && "rotate-180",
                )}
              />
            </button>
            <button
              type="button"
              title={row.original.name}
              onClick={() => onShowOnMap(neighborhoodId)}
              className="min-w-0 cursor-pointer truncate text-start font-medium text-text transition-colors duration-150 ease-out hover:text-action hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
            >
              {row.original.name}
            </button>
            <span className="hidden text-xs font-normal text-text-secondary sm:inline">
              {row.original.districtName}
            </span>
          </span>
        );
      },
    }),
    ...visibleMetrics.map((metric) =>
      columnHelper.accessor((row) => row.values[metric.key]?.raw, {
        id: metric.key,
        header: () => localizedMetricName(metric, locale),
        enableGlobalFilter: false,
        sortUndefined: "last" as const,
        cell: ({ row, table }) => (
          <MetricCell
            value={row.original.values[metric.key]}
            metric={metric}
            locale={locale}
            highlighted={sortedMetricKey === metric.key}
            featured={
              highlightsMerit &&
              sortedMetricKey === metric.key &&
              table.getRowModel().rows.indexOf(row) < 3
            }
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
        <button
          type="button"
          onClick={() => onShowOnMap(row.original.neighborhoodId)}
          className="cursor-pointer whitespace-nowrap text-xs font-medium text-text-secondary transition-colors duration-150 ease-out hover:text-action hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
        >
          <fbt desc="Button opening the map view from a data row">Voir sur la carte</fbt>
        </button>
      ),
    }),
  ]);
}
