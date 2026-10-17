import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { MapView } from "./map-view";

function renderMap({ selectedNeighborhood }: { selectedNeighborhood: string | null }) {
  return renderToStaticMarkup(
    <MapView
      criteria={{ weights: [] }}
      onRemoveCriterion={() => {}}
      selectedNeighborhood={selectedNeighborhood}
      onSelectNeighborhood={() => {}}
    />,
  );
}

function rankingSection(markup: string) {
  return markup.slice(markup.indexOf("<ol"), markup.indexOf("</ol>"));
}

describe("map view", () => {
  it("names the map region for assistive technology", () => {
    const markup = renderMap({ selectedNeighborhood: null });

    expect(markup).toContain('role="region"');
    expect(markup).toContain('aria-labelledby="map-title"');
    expect(markup).toContain('<h1 id="map-title"');
  });

  it("lets every ranking row be selected", () => {
    const ranking = rankingSection(renderMap({ selectedNeighborhood: null }));

    expect(ranking.match(/<button/g)).toHaveLength(3);
    expect(ranking.match(/aria-pressed="false"/g)).toHaveLength(3);
  });

  it("marks the neighborhood handed over by another view", () => {
    const ranking = rankingSection(renderMap({ selectedNeighborhood: "Cihangir" }));

    expect(ranking.match(/aria-pressed="true"/g)).toHaveLength(1);
    expect(ranking).toContain("Cihangir");
    expect(ranking.slice(ranking.indexOf('aria-pressed="true"'))).toContain("Cihangir");
  });

  it("shows the map attribution", () => {
    const markup = renderMap({ selectedNeighborhood: null });

    expect(markup).toContain("OpenFreeMap");
    expect(markup).toContain("OpenStreetMap");
  });

  it("keeps the score ramp legible and distinguishes the missing value", () => {
    const markup = renderMap({ selectedNeighborhood: null });

    expect(markup.match(/bg-ramp-\d/g)).toHaveLength(5);
    expect(markup.match(/ring-1 ring-border/g)?.length).toBeGreaterThanOrEqual(6);
    expect(markup).toContain("no-data-pattern");
  });
});
