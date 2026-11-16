import { fbs } from "fbtee";
import { Input } from "../../components/ui/input";

export function DataTableToolbar({
  searchText,
  shownCount,
  totalCount,
  onSearchChange,
}: {
  searchText: string;
  shownCount: number;
  totalCount: number;
  onSearchChange: (value: string) => void;
}) {
  return (
    <div className="mt-8 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <label className="w-full max-w-sm">
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
        />
      </label>
      <p role="status" className="text-xs text-text-secondary">
        <fbt desc="Count of neighborhoods currently displayed in the raw values table">
          Quartiers affichés :
        </fbt>{" "}
        <span className="font-mono tabular-nums">
          {shownCount} / {totalCount}
        </span>
      </p>
    </div>
  );
}
