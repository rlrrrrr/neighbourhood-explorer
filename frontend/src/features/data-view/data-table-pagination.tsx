import { fbs } from "fbtee";
import { ChevronLeft, ChevronRight, ChevronsLeft } from "lucide-react";
import { Button } from "../../components/ui/button";

export function DataTablePagination({
  pageIndex,
  pageCount,
  canPreviousPage,
  canNextPage,
  onPreviousPage,
  onNextPage,
  onFirstPage,
}: {
  pageIndex: number;
  pageCount: number;
  canPreviousPage: boolean;
  canNextPage: boolean;
  onPreviousPage: () => void;
  onNextPage: () => void;
  onFirstPage: () => void;
}) {
  const isLastPage = pageCount > 1 && pageIndex === pageCount - 1;

  return (
    <nav
      aria-label={fbs(
        "Pagination du tableau des quartiers",
        "Accessible label of the data table pagination",
      )}
      className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-border pt-3"
    >
      <p className="text-xs text-text-secondary">
        <fbt desc="Label of the current page in the data table pagination">Page</fbt>{" "}
        <span dir="ltr">
          <span className="tabular-nums">{pageIndex + 1}</span>
          <span> / </span>
          <span className="tabular-nums">{pageCount}</span>
        </span>
      </p>
      <div className="flex items-center gap-2 max-sm:w-full max-sm:justify-between">
        <Button variant="ghost" size="sm" disabled={!canPreviousPage} onClick={onPreviousPage}>
          <ChevronLeft aria-hidden="true" className="size-4 rtl:rotate-180" />
          <fbt desc="Button showing the previous page of the data table">Précédent</fbt>
        </Button>
        {isLastPage ? (
          <Button variant="ghost" size="sm" onClick={onFirstPage}>
            <ChevronsLeft aria-hidden="true" className="size-4 rtl:rotate-180" />
            <span className="sm:hidden">
              <fbt desc="Short label of the button returning to the first page of the data table">
                Début
              </fbt>
            </span>
            <span className="hidden sm:inline">
              <fbt desc="Button returning to the first page of the data table">
                Revenir au début
              </fbt>
            </span>
          </Button>
        ) : (
          <Button variant="ghost" size="sm" disabled={!canNextPage} onClick={onNextPage}>
            <fbt desc="Button showing the next page of the data table">Suivant</fbt>
            <ChevronRight aria-hidden="true" className="size-4 rtl:rotate-180" />
          </Button>
        )}
      </div>
    </nav>
  );
}
