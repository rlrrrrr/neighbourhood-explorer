import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { CriteriaChips } from "./criteria-chips";
import { EMPTY_CRITERIA } from "./criteria";

const CAFES = renderToStaticMarkup(
  <CriteriaChips criteria={{ weights: [{ metricKey: "cafes", weight: 1 }] }} onRemove={() => {}} />,
);

describe("criteria chips", () => {
  it("labels a known metric and offers a remove control", () => {
    expect(CAFES).toContain("Cafés");
    expect(CAFES).toContain('aria-label="Retirer ce critère"');
    expect(CAFES.match(/<li /g)).toHaveLength(1);
  });

  it("falls back to the raw key of a metric it cannot label", () => {
    const markup = renderToStaticMarkup(
      <CriteriaChips
        criteria={{ weights: [{ metricKey: "quietness", weight: 0.6 }] }}
        onRemove={() => {}}
      />,
    );

    expect(markup).toContain("quietness");
  });

  it("renders an empty list without a criterion", () => {
    const markup = renderToStaticMarkup(
      <CriteriaChips criteria={EMPTY_CRITERIA} onRemove={() => {}} />,
    );

    expect(markup).not.toContain("<li");
  });
});
