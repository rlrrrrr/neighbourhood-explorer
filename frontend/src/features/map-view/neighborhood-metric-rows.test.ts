import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import type { components } from "../../generated/api";
import {
  buildNeighborhoodMetricRows,
  filterNeighborhoodMetricRows,
  type NeighborhoodMetricRow,
} from "./neighborhood-metric-rows";

type MetricDefinition = components["schemas"]["MetricDefinition"];
type NeighborhoodMetric = components["schemas"]["NeighborhoodMetric"];

const CAFES: MetricDefinition = {
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
};

const GREENERY: MetricDefinition = {
  ...CAFES,
  key: "greenery",
  category: "Environment",
  unit: "% de la surface",
  displayPrecision: 1,
  translations: [
    {
      locale: "fr",
      name: "Verdure",
      description: "Part de la surface verte.",
      subject: "la verdure",
    },
  ],
};

const CAFES_VALUE: NeighborhoodMetric = {
  key: "cafes_density",
  raw: 173.37259956981498,
  normalized: 0.9885773624091381,
  qualityFlag: "ok",
  observedAt: "2026-10-30",
  referencePeriod: "2026-10-30",
  source: {
    id: "osm-geofabrik-istanbul",
    name: "OpenStreetMap",
    organization: "OpenStreetMap contributors",
    license: "ODbL 1.0",
    attribution: "© les contributeurs d'OpenStreetMap",
  },
};

describe("neighborhood metric rows", () => {
  it("joins every published definition with the neighborhood value", () => {
    const rows = buildNeighborhoodMetricRows({
      definitions: [CAFES, GREENERY],
      metrics: [CAFES_VALUE],
      locale: "fr",
    });

    expect(rows).toHaveLength(2);
    expect(rows[0]).toEqual({
      key: "cafes_density",
      name: "Densité de cafés",
      category: "Vie locale",
      unit: "cafés/km²",
      displayPrecision: 1,
      raw: 173.37259956981498,
      normalized: 0.9885773624091381,
      qualityFlag: "ok",
      observedAt: "2026-10-30",
      referencePeriod: "2026-10-30",
      sourceName: "OpenStreetMap",
    });
  });

  it("keeps a definition without a value instead of dropping the metric", () => {
    const rows = buildNeighborhoodMetricRows({
      definitions: [CAFES, GREENERY],
      metrics: [CAFES_VALUE],
      locale: "fr",
    });

    expect(rows[1]).toMatchObject({
      key: "greenery",
      name: "Verdure",
      raw: null,
      normalized: null,
      qualityFlag: null,
      referencePeriod: null,
      observedAt: "2026-10-30",
      sourceName: "OpenStreetMap",
    });
  });
});

const ROWS: NeighborhoodMetricRow[] = [
  {
    key: "cafes_density",
    name: "Densité de cafés",
    category: "Vie locale",
    unit: "cafés/km²",
    displayPrecision: 1,
    raw: 173.37,
    normalized: 0.98,
    qualityFlag: "ok",
    observedAt: "2026-10-30",
    referencePeriod: "2026-10-30",
    sourceName: "OpenStreetMap",
  },
  {
    key: "greenery",
    name: "Verdure",
    category: "Environnement",
    unit: "% de la surface",
    displayPrecision: 1,
    raw: 17.3,
    normalized: 0.6,
    qualityFlag: "ok",
    observedAt: "2021-12-31",
    referencePeriod: "2021-12-31",
    sourceName: "ESA WorldCover 2021",
  },
];

describe("neighborhood metric filter", () => {
  it("returns every metric when the query is blank", () => {
    expect(filterNeighborhoodMetricRows({ rows: ROWS, query: "   ", locale: "fr" })).toEqual(ROWS);
  });

  it("matches a metric name regardless of case", () => {
    const filtered = filterNeighborhoodMetricRows({ rows: ROWS, query: "CAFÉS", locale: "fr" });

    expect(filtered.map((row) => row.key)).toEqual(["cafes_density"]);
  });

  it("returns nothing when no metric matches", () => {
    expect(filterNeighborhoodMetricRows({ rows: ROWS, query: "plage", locale: "fr" })).toEqual([]);
  });
});
