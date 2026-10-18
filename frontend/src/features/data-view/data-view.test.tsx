import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { DataView } from "./data-view";

const MARKUP = renderToStaticMarkup(
  <DataView
    criteria={{ weights: [] }}
    onRemoveCriterion={() => {}}
    sort={null}
    onSortChange={() => {}}
    onShowOnMap={() => {}}
  />,
);

describe("data view", () => {
  it("offers a sort control per criterion column", () => {
    expect(MARKUP.match(/aria-sort="none"/g)).toHaveLength(3);
    expect(MARKUP).toContain("Cafés");
    expect(MARKUP).toContain("Métro à pied");
    expect(MARKUP).toContain("Verdure");
  });

  it("repeats the unit next to every value so it survives a scroll", () => {
    expect(MARKUP.match(/\/km²/g)).toHaveLength(3);
    expect(MARKUP.match(/>min</g)).toHaveLength(3);
    expect(MARKUP.match(/>%</g)).toHaveLength(3);
  });

  it("freezes the neighborhood column and the units in a header row", () => {
    expect(MARKUP).toContain("sticky start-0");
    expect(MARKUP).toContain('scope="row"');
    expect(MARKUP.match(/scope="col"/g)).toHaveLength(6);
  });

  it("never highlights a row as the best one", () => {
    const rows = MARKUP.slice(MARKUP.indexOf("<tbody>"), MARKUP.indexOf("</tbody>"));

    expect(rows).not.toContain("bg-highlight");
    expect(MARKUP).toContain("odd:bg-surface");
  });

  it("shows the active criteria above the table, and nothing when there are none", () => {
    expect(MARKUP).not.toContain("Critères actifs");

    const markup = renderToStaticMarkup(
      <DataView
        criteria={{ weights: [{ metricKey: "cafes", weight: 1 }] }}
        onRemoveCriterion={() => {}}
        sort={null}
        onSortChange={() => {}}
        onShowOnMap={() => {}}
      />,
    );

    expect(markup).toContain("Critères actifs");
    expect(markup).toContain("Retirer ce critère");
  });

  it("sends the row neighborhood to the map", () => {
    const neighborhoods = ["Moda", "Cihangir", "Yeldeğirmeni"];
    for (const name of neighborhoods) {
      expect(MARKUP).toContain(name);
    }
    expect(MARKUP.match(/Voir sur la carte/g)).toHaveLength(3);
  });

  it("sorts the column and the rows given by the URL", () => {
    const markup = renderToStaticMarkup(
      <DataView
        criteria={{ weights: [] }}
        onRemoveCriterion={() => {}}
        sort={{ key: "cafes", direction: "descending" }}
        onSortChange={() => {}}
        onShowOnMap={() => {}}
      />,
    );

    expect(markup).toContain('aria-sort="descending"');
    expect(markup.match(/aria-sort="none"/g)).toHaveLength(2);

    const rows = markup.slice(markup.indexOf("<tbody>"), markup.indexOf("</tbody>"));
    expect(rows.indexOf("Moda")).toBeLessThan(rows.indexOf("Cihangir"));
    expect(rows.indexOf("Cihangir")).toBeLessThan(rows.indexOf("Yeldeğirmeni"));
  });
});
