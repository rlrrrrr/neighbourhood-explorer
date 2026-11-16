import { fbt } from "fbtee";
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
import { Button } from "../../components/ui/button";
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
  sortedMetricKey,
  onShowOnMap,
}: {
  data: DataTableData;
  locale: string;
  sortedMetricKey: string | undefined;
  onShowOnMap: (neighborhoodId: number) => void;
}) {
  return columnHelper.columns([
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
            highlighted={sortedMetricKey === metric.key}
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
  ]);
}
