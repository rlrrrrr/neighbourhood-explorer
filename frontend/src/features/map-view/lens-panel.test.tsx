import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import type { MetricDefinition } from "../metrics/metric-api-schema";
import { LensPanel } from "./lens-panel";

const metrics: readonly MetricDefinition[] = [
  {
    key: "population_density",
    category: "People",
    unit: "people/km²",
    nativeLevel: "neighborhood",
    polarity: "neutral",
    normalization: { method: "min_max", version: "1" },
    displayPrecision: 0,
    translations: [
      {
        locale: "fr",
        name: "Densité de population",
        description: "Population par kilomètre carré.",
        subject: "la densité de population",
      },
    ],
    source: {
      id: "worldpop",
      name: "WorldPop",
      organization: "WorldPop",
      url: "https://www.worldpop.org/",
      license: "CC BY 4.0",
      attribution: "WorldPop 2025",
      methodology: "Estimation",
      retrievedAt: "2026-01-01",
      version: "2025",
    },
    observedAt: "2025-12-31",
  },
  {
    key: "cafes_density",
    category: "Lifestyle",
    unit: "cafés/km²",
    nativeLevel: "neighborhood",
    polarity: "higher_is_better",
    normalization: { method: "percentile", version: "percentile-v1" },
    displayPrecision: 1,
    translations: [
      {
        locale: "fr",
        name: "Densité de cafés",
        description: "Cafés par kilomètre carré.",
        subject: "les cafés",
      },
    ],
    source: {
      id: "osm-geofabrik-istanbul",
      name: "OpenStreetMap",
      organization: "OpenStreetMap contributors",
      url: "https://www.openstreetmap.org/",
      license: "ODbL 1.0",
      attribution: "© les contributeurs d'OpenStreetMap",
      methodology: "Cafés OSM par kilomètre carré.",
      retrievedAt: "2026-10-31",
      version: "turkey-261030.osm.pbf",
    },
    observedAt: "2026-10-30",
  },
];

function renderLens({
  criteriaKeys = [],
  open = true,
  selectedMetricKey = "population_density",
}: {
  criteriaKeys?: readonly string[];
  open?: boolean;
  selectedMetricKey?: string;
} = {}) {
  return renderToStaticMarkup(
    <LensPanel
      cityName="Ville de test"
      observedAt="2025-12-31"
      criteria={{ weights: criteriaKeys.map((metricKey) => ({ metricKey, weight: 1 })) }}
      onRemoveCriterion={() => {}}
      metrics={metrics}
      selectedMetricKey={selectedMetricKey}
      onSelectMetric={() => {}}
      open={open}
      onClose={() => {}}
    />,
  );
}

describe("lens panel", () => {
  it("renders the lens sections and a close control", () => {
    const markup = renderLens();

    expect(markup).toContain('aria-label="La lentille"');
    expect(markup).toContain("Ville de test");
    expect(markup).toContain("2025-12-31");
    expect(markup).toContain("Métrique affichée");
    expect(markup).toContain("Catégories");
    expect(markup).toContain("Sélection");
    expect(markup).toContain("Fermer le panneau de la lentille");
  });

  it("offers the categories published by the API, translated and unchecked", () => {
    const markup = renderLens();

    expect(markup).toContain("Population");
    expect(markup).toContain("Vie locale");
    expect(markup.match(/type="checkbox"/g)).toHaveLength(2);
    expect(markup).not.toMatch(/type="checkbox"[^>]*checked/);
  });

  it("checks the metric currently driving the map", () => {
    const markup = renderLens({ selectedMetricKey: "cafes_density" });

    expect(markup).toContain('name="lens-metric" checked="" value="cafes_density"');
    expect(markup).toContain("Densité de cafés");
    expect(markup).toContain("Densité de population");
  });

  it("keeps the metric controls reachable by keyboard under a styled indicator", () => {
    const markup = renderLens();

    expect(markup.match(/peer sr-only/g)).toHaveLength(4);
    expect(markup).toContain("has-[:checked]:bg-highlight");
    expect(markup).toContain("peer-checked:opacity-100");
  });

  it("wraps the filtered metrics in an animated height container", () => {
    const markup = renderLens();

    expect(markup).toContain("lens-metric-list");
    expect(markup).toContain("lens-metric-list-content");
  });

  it("clips the panel so focusing a filter cannot scroll its header away", () => {
    const markup = renderLens();

    expect(markup).toMatch(/<aside[^>]*class="[^"]*overflow-clip/);
  });

  it("shows the criteria of the shared state, with its empty state", () => {
    expect(renderLens()).toContain("Aucun critère actif");

    const markup = renderLens({ criteriaKeys: ["cafes"] });
    expect(markup).toContain("Critères");
    expect(markup).toContain("Cafés");
    expect(markup).toContain("Retirer ce critère");
    expect(markup).not.toContain("Aucun critère actif");
  });

  it("stays mounted but inert while closed, so its controls leave the tab order", () => {
    const markup = renderLens({ open: false });

    expect(markup).toContain("anchored-panel");
    expect(markup).toContain("inert");
    expect(markup).not.toContain("data-open");
  });
});
