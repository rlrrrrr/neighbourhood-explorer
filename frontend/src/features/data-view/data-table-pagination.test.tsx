import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { DataTablePagination } from "./data-table-pagination";

function renderPagination(pageIndex: number): string {
  return renderToStaticMarkup(
    <DataTablePagination
      pageIndex={pageIndex}
      pageCount={20}
      canPreviousPage={pageIndex > 0}
      canNextPage={pageIndex < 19}
      onPreviousPage={() => {}}
      onNextPage={() => {}}
      onFirstPage={() => {}}
    />,
  );
}

describe("data table pagination", () => {
  it("shows the page position and the next page in the middle of the list", () => {
    const markup = renderPagination(1);

    expect(markup).toContain("Page");
    expect(markup).toContain(">2</span>");
    expect(markup).toContain("Suivant");
    expect(markup).not.toContain("Revenir au début");
  });

  it("offers a way back to the first page on the last page", () => {
    const markup = renderPagination(19);

    expect(markup).toContain("Revenir au début");
    expect(markup).not.toContain("Suivant");
    expect(markup).toContain(">20</span>");
  });
});
