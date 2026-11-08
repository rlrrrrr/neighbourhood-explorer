import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { MapView } from "./map-view";

function renderMap({ selectedNeighborhoodId }: { selectedNeighborhoodId: number | null }) {
  return renderToStaticMarkup(
    <MapView
      criteria={{ weights: [] }}
      onRemoveCriterion={() => {}}
      selectedNeighborhoodId={selectedNeighborhoodId}
      onSelectNeighborhood={() => {}}
    />,
  );
}

describe("map view", () => {
  it("names the map region for assistive technology", () => {
    const markup = renderMap({ selectedNeighborhoodId: null });

    expect(markup).toContain('role="region"');
    expect(markup).toContain('aria-labelledby="map-title"');
    expect(markup).toContain('<h1 id="map-title"');
  });

  it("shows the loading state while the map data arrives", () => {
    const markup = renderMap({ selectedNeighborhoodId: null });

    expect(markup).toContain("Chargement des quartiers…");
  });

  it("keeps the score ramp legible and distinguishes the missing value", () => {
    const markup = renderMap({ selectedNeighborhoodId: null });

    expect(markup.match(/bg-ramp-\d/g)).toHaveLength(5);
    expect(markup.match(/ring-1 ring-border/g)?.length).toBeGreaterThanOrEqual(6);
    expect(markup).toContain("no-data-pattern");
  });
});
