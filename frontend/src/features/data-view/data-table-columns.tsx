import { fbs, fbt } from "fbtee";
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
import { Map } from "lucide-react";
import { localizedMetricName } from "../metrics/metric-presentation";
import type { DataTableData, DataTableRow } from "./data-table-client";
import type { MetricQualityFlag } from "./data-table-quality";
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
  dominantQualityFlags,
  highlightsMerit,
  showDistrict,
  onShowOnMap,
}: {
  data: DataTableData;
  locale: string;
  visibleMetricKeys: readonly string[];
  sortedMetricKey: string | undefined;
  dominantQualityFlags: ReadonlyMap<string, MetricQualityFlag>;
  highlightsMerit: boolean;
  showDistrict: boolean;
  onShowOnMap: (neighborhoodId: number) => void;
}) {
  const visibleMetrics = visibleMetricKeys.flatMap((key) => {
    const metric = data.metrics.find((candidate) => candidate.key === key);

    return metric === undefined ? [] : [metric];
  });

  return columnHelper.columns([
    columnHelper.accessor((row) => `${row.name} ${row.districtName}`, {
      id: "name",
      header: () => fbt("Quartier", "Column header of the neighborhood name"),
      enableSorting: false,
      cell: ({ row }) => (
        <span className="flex min-w-0 items-baseline gap-2">
          <button
            type="button"
            title={row.original.name}
            onClick={() => onShowOnMap(row.original.neighborhoodId)}
            className="min-w-0 w-full max-w-[6.5rem] cursor-pointer truncate text-start font-medium text-text transition-colors duration-150 ease-out hover:text-action focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action sm:w-auto sm:max-w-[16rem]"
          >
            {row.original.name}
          </button>
          {showDistrict && (
            <span className="hidden shrink-0 text-[11px] font-normal text-text-secondary sm:inline">
              {row.original.districtName}
            </span>
          )}
        </span>
      ),
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
            dominantQualityFlag={dominantQualityFlags.get(metric.key)}
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
      cell: ({ row }) => {
        const label = fbs("Voir sur la carte", "Button opening the map view from a data row");

        return (
          <button
            type="button"
            aria-label={label}
            title={label}
            onClick={() => onShowOnMap(row.original.neighborhoodId)}
            className="inline-flex size-8 cursor-pointer items-center justify-center rounded-md text-text-secondary transition-colors duration-150 ease-out hover:bg-bg hover:text-action focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
          >
            <Map aria-hidden="true" className="size-4" />
          </button>
        );
      },
    }),
  ]);
}
