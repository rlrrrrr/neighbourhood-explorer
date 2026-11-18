import { fbs } from "fbtee";
import { Check, ChevronDown } from "lucide-react";
import type { ReactNode } from "react";
import { cn } from "../../components/ui/cn";
import { Input } from "../../components/ui/input";
import type { MetricDefinition } from "../metrics/metric-api-schema";
import { localizedMetricName } from "../metrics/metric-presentation";
import { selectedMetricSort } from "./metric-sort-choice";
import type { TableSort } from "./table-sort";

export function DataTableToolbar({
  searchText,
  shownCount,
  totalCount,
  metrics,
  locale,
  sort,
  groupByDistrict,
  showGroupAverage,
  onToggleGrouping,
  onToggleAverage,
  onSearchChange,
  onSortChange,
}: {
  searchText: string;
  shownCount: number;
  totalCount: number;
  metrics: readonly MetricDefinition[];
  locale: string;
  sort: TableSort | null;
  groupByDistrict: boolean;
  showGroupAverage: boolean;
  onToggleGrouping: () => void;
  onToggleAverage: () => void;
  onSearchChange: (value: string) => void;
  onSortChange: (sort: TableSort | null) => void;
}) {
  return (
    <div className="mt-5 flex flex-wrap items-center gap-2">
      <label className="block w-full max-w-[15rem]">
        <span className="sr-only">
          <fbt desc="Label of the search field filtering the raw values table">
            Rechercher un quartier
          </fbt>
        </span>
        <Input
          type="search"
          value={searchText}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder={fbs(
            "Nom du quartier ou du district",
            "Placeholder of the raw values table search field",
          )}
          className="h-8 text-xs"
        />
      </label>

      <label className="flex w-full max-w-[18rem] items-center gap-2">
        <span className="shrink-0 text-[11px] text-text-secondary">
          <fbt desc="Label of the metric selector that sorts the data table">Trier par</fbt>
        </span>
        <span className="group relative min-w-0 flex-1">
          <select
            value={sort?.key ?? ""}
            onChange={(event) =>
              onSortChange(selectedMetricSort({ metricKey: event.target.value, metrics }))
            }
            className="h-8 w-full appearance-none rounded-lg border border-border bg-surface ps-2.5 pe-8 text-[11px] text-text transition-[border-color] duration-[160ms] ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action [&:open]:border-action"
          >
            <option value="">
              <fbt desc="Alphabetical order option in the data table metric selector">
                Ordre alphabétique
              </fbt>
            </option>
            {metrics.map((metric) => (
              <option key={metric.key} value={metric.key}>
                {localizedMetricName(metric, locale)}
              </option>
            ))}
          </select>
          <ChevronDown
            aria-hidden="true"
            className="pointer-events-none absolute end-2.5 top-1/2 size-3.5 -translate-y-1/2 text-text-secondary"
          />
        </span>
      </label>

      <div
        role="group"
        aria-label={fbs(
          "Affichage du tableau",
          "Accessible label of the display options of the data table",
        )}
        className="flex items-center gap-1.5"
      >
        <ToolbarToggle pressed={groupByDistrict} onToggle={onToggleGrouping}>
          <fbt desc="Button grouping the data table rows by district">Grouper par district</fbt>
        </ToolbarToggle>
        <ToolbarToggle pressed={showGroupAverage} onToggle={onToggleAverage}>
          <fbt desc="Button showing the metric average in each district group">
            Moyenne du district
          </fbt>
        </ToolbarToggle>
      </div>

      <p role="status" className="ms-auto text-[11px] text-text-secondary tabular-nums">
        <fbt desc="Count of neighborhoods currently displayed in the raw values table">
          Quartiers affichés :
        </fbt>{" "}
        <span dir="ltr">
          {shownCount} / {totalCount}
        </span>
      </p>
    </div>
  );
}

function ToolbarToggle({
  pressed,
  onToggle,
  children,
}: {
  pressed: boolean;
  onToggle: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      onClick={onToggle}
      className={cn(
        "flex h-8 cursor-pointer items-center gap-1 rounded-lg px-2.5 text-[11px] text-text-secondary transition-colors duration-150 ease-out hover:bg-surface/60 hover:text-text focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action",
        pressed && "bg-action/[0.06] text-text ring-1 ring-border/60 ring-inset",
      )}
    >
      <span className="grid size-3.5 shrink-0 place-items-center">
        {pressed ? <Check aria-hidden="true" className="size-3.5" /> : null}
      </span>
      {children}
    </button>
  );
}
