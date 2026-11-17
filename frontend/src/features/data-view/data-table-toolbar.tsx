import { fbs } from "fbtee";
import { cn } from "../../components/ui/cn";
import { Input } from "../../components/ui/input";

export function DataTableToolbar({
  searchText,
  shownCount,
  totalCount,
  groupByDistrict,
  showGroupAverage,
  onToggleGrouping,
  onToggleAverage,
  onSearchChange,
}: {
  searchText: string;
  shownCount: number;
  totalCount: number;
  groupByDistrict: boolean;
  showGroupAverage: boolean;
  onToggleGrouping: () => void;
  onToggleAverage: () => void;
  onSearchChange: (value: string) => void;
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
      <div className="flex items-center gap-1.5">
        <button
          type="button"
          aria-pressed={groupByDistrict}
          onClick={onToggleGrouping}
          className={cn(
            "h-8 cursor-pointer rounded-lg px-2.5 text-[11px] text-text-secondary transition-colors duration-150 ease-out hover:bg-surface/60 hover:text-text focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action",
            groupByDistrict && "bg-action/[0.06] text-text ring-1 ring-border/60 ring-inset",
          )}
        >
          <fbt desc="Button grouping the data table rows by district">Regrouper</fbt>
        </button>
        <button
          type="button"
          aria-pressed={showGroupAverage}
          onClick={onToggleAverage}
          className={cn(
            "h-8 cursor-pointer rounded-lg px-2.5 text-[11px] text-text-secondary transition-colors duration-150 ease-out hover:bg-surface/60 hover:text-text focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action",
            showGroupAverage && "bg-action/[0.06] text-text ring-1 ring-border/60 ring-inset",
          )}
        >
          <fbt desc="Button showing the average of each district group">Moyennes</fbt>
        </button>
      </div>
      <p role="status" className="ms-auto text-[11px] text-text-secondary tabular-nums">
        <fbt desc="Count of neighborhoods currently displayed in the raw values table">
          Quartiers affichés :
        </fbt>{" "}
        <span>
          {shownCount} / {totalCount}
        </span>
      </p>
    </div>
  );
}
