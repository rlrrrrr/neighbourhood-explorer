import { fbt, type TranslatedString } from "fbtee";
import { Button } from "../../components/ui/button";
import { cn } from "../../components/ui/cn";
import { useScrollOverflowHint } from "../../components/ui/scroll-affordance";
import {
  SAMPLE_NEIGHBORHOODS,
  type SampleNeighborhood,
} from "../neighborhood-detail/sample-neighborhoods";
import { nextSort, sortRows, type TableSort } from "./table-sort";

interface DataViewProps {
  sort: TableSort | null;
  onSortChange: (sort: TableSort) => void;
  onShowOnMap: (neighborhood: string) => void;
}

interface SampleColumn {
  key: string;
  header: () => TranslatedString;
  unit: () => TranslatedString;
  value: (row: SampleNeighborhood) => string;
  sortValue: (row: SampleNeighborhood) => number;
}

const SAMPLE_COLUMNS: readonly SampleColumn[] = [
  {
    key: "cafes",
    header: () => fbt("Cafés", "Column header of the cafés per square kilometer"),
    unit: () => fbt("/km²", "Unit suffix of the café density column"),
    value: (row) => row.cafes,
    sortValue: (row) => Number.parseFloat(row.cafes.replace(",", ".")),
  },
  {
    key: "metro",
    header: () => fbt("Métro à pied", "Column header of the walking distance to the metro"),
    unit: () => fbt("min", "Unit suffix of the walking distance to the metro column"),
    value: (row) => row.metro.replace(/\D/g, ""),
    sortValue: (row) => Number.parseInt(row.metro, 10),
  },
  {
    key: "green",
    header: () => fbt("Verdure", "Column header of the green cover"),
    unit: () => fbt("%", "Unit suffix of the green cover column"),
    value: (row) => row.green.replace(/\D/g, ""),
    sortValue: (row) => Number.parseInt(row.green, 10),
  },
];

function sortValueOf(row: SampleNeighborhood, key: string): number {
  const column = SAMPLE_COLUMNS.find((candidate) => candidate.key === key);
  return column ? column.sortValue(row) : 0;
}

function SortArrow({ direction }: { direction: TableSort["direction"] | null }) {
  return (
    <svg
      viewBox="0 0 12 12"
      aria-hidden="true"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={cn(
        "size-3 shrink-0 transition-[opacity,transform] duration-150 ease-out motion-reduce:transition-none",
        direction ? "opacity-100" : "opacity-0",
        direction === "descending" && "rotate-180",
      )}
    >
      <path d="M6 9.5V2.5" />
      <path d="M3 5.5 6 2.5l3 3" />
    </svg>
  );
}

export function DataView({ sort, onSortChange, onShowOnMap }: DataViewProps) {
  const { containerRef, hasMore, overflows } = useScrollOverflowHint({ axis: "horizontal" });
  const rows = sortRows({ rows: SAMPLE_NEIGHBORHOODS, sort, sortValueOf });
  const stickyColumn = cn("sticky start-0 z-10 bg-inherit", overflows && "border-e");

  return (
    <div className="mx-auto w-full max-w-4xl flex-1 px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight">
        <fbt desc="Title of the data view">Valeurs brutes</fbt>
      </h1>
      <p className="mt-2 text-sm text-text-secondary">
        <fbt desc="Subtitle of the data view, with the year and the raw-value promise">
          Millésime 2024 · valeurs brutes avec unité, sans score composite
        </fbt>
      </p>
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
                <th scope="col" className="px-4 py-3">
                  <fbt desc="Column header of the district name">District</fbt>
                </th>
                {SAMPLE_COLUMNS.map((column) => {
                  const direction = sort?.key === column.key ? sort.direction : null;
                  return (
                    <th
                      key={column.key}
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
                        onClick={() => onSortChange(nextSort(sort, column.key))}
                        className="flex min-h-11 w-full cursor-pointer items-center justify-end gap-1 px-4 py-3 font-semibold transition-colors duration-150 ease-out hover:text-text focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-action sm:min-h-0"
                      >
                        {column.header()}
                        <SortArrow direction={direction} />
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
                <tr key={row.name} className="even:bg-bg odd:bg-surface">
                  <th
                    scope="row"
                    className={cn(stickyColumn, "border-border px-4 py-3 text-left font-semibold")}
                  >
                    {row.name}
                  </th>
                  <td className="px-4 py-3 text-text-secondary">{row.district}</td>
                  {SAMPLE_COLUMNS.map((column) => (
                    <td key={column.key} className="px-4 py-3 text-right">
                      <span className="tabular-nums">{column.value(row)}</span>
                      <span className="ml-1 text-xs text-text-secondary">{column.unit()}</span>
                    </td>
                  ))}
                  <td className="px-4 py-3 text-right">
                    <Button variant="outline" size="sm" onClick={() => onShowOnMap(row.name)}>
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
    </div>
  );
}
