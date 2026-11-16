import { fbs } from "fbtee";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "../../components/ui/button";

export function DataTablePagination({
  pageIndex,
  pageCount,
  canPreviousPage,
  canNextPage,
  onPreviousPage,
  onNextPage,
}: {
  pageIndex: number;
  pageCount: number;
  canPreviousPage: boolean;
  canNextPage: boolean;
  onPreviousPage: () => void;
  onNextPage: () => void;
}) {
  return (
    <nav
      aria-label={fbs(
        "Pagination du tableau des quartiers",
        "Accessible label of the data table pagination",
      )}
      className="mt-4 flex items-center justify-between gap-3 border-t border-border pt-4"
    >
      <p className="text-xs text-text-secondary">
        <fbt desc="Label of the current page in the data table pagination">Page</fbt>{" "}
        <span className="font-mono tabular-nums">{pageIndex + 1}</span>
        <span> / </span>
        <span className="font-mono tabular-nums">{pageCount}</span>
      </p>
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="sm" disabled={!canPreviousPage} onClick={onPreviousPage}>
          <ChevronLeft aria-hidden="true" className="size-4 rtl:rotate-180" />
          <fbt desc="Button showing the previous page of the data table">Précédent</fbt>
        </Button>
        <Button variant="ghost" size="sm" disabled={!canNextPage} onClick={onNextPage}>
          <fbt desc="Button showing the next page of the data table">Suivant</fbt>
          <ChevronRight aria-hidden="true" className="size-4 rtl:rotate-180" />
        </Button>
      </div>
    </nav>
  );
}
