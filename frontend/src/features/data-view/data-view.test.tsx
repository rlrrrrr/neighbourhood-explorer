import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import type { components } from "../../generated/api";
import type { Criteria } from "../criteria/criteria";
import { DataTableView } from "./data-table-view";
import type { DataTableData } from "./data-table-client";
import type { DataTableState } from "./use-data-table";

type MetricDefinition = components["schemas"]["MetricDefinition"];

const GREENERY: MetricDefinition = {
  key: "greenery",
  category: "Environment",
  unit: "% de la surface",
  nativeLevel: "neighborhood",
  polarity: "higher_is_better",
  normalization: { method: "percentile", version: "percentile-v1" },
  displayPrecision: 1,
  translations: [
    {
      locale: "fr",
      name: "Verdure",
      description: "Part de la surface verte.",
      subject: "la verdure",
    },
  ],
  source: {
    id: "esa-worldcover",
    name: "ESA WorldCover 2021",
    organization: "ESA WorldCover consortium",
    url: "https://esa-worldcover.org/",
    license: "CC BY 4.0 — https://creativecommons.org/licenses/by/4.0/",
    attribution: "© ESA WorldCover project 2021",
    methodology: "Part de la surface classée verte dans la tuile.",
    retrievedAt: "2026-11-09",
    version: "WorldCover 2021 v200",
  },
  observedAt: "2021-12-31",
};

const CAFES_DENSITY: MetricDefinition = {
  ...GREENERY,
  key: "cafes_density",
  category: "Lifestyle",
  unit: "cafés/km²",
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
    name: "OpenStreetMap, extrait Geofabrik Turquie",
    organization: "OpenStreetMap contributors / Geofabrik GmbH",
    url: "https://download.geofabrik.de/europe/turkey.html",
    license: "ODbL 1.0 — https://opendatacommons.org/licenses/odbl/1-0/",
    attribution: "© les contributeurs d'OpenStreetMap, sous licence ODbL",
    methodology: "Cafés OSM par kilomètre carré.",
    retrievedAt: "2026-10-31",
    version: "turkey-261030.osm.pbf",
  },
  observedAt: "2026-10-30",
};

const DATA: DataTableData = {
  metrics: [CAFES_DENSITY, GREENERY],
  rows: [
    {
      neighborhoodId: 1570,
      name: "Caferağa Mahallesi",
      districtName: "Kadıköy",
      values: {
        cafes_density: { raw: 173.4, qualityFlag: "ok" },
        greenery: { raw: 17.3, qualityFlag: "estimated" },
      },
    },
    {
      neighborhoodId: 1272,
      name: "Cihangir Mahallesi",
      districtName: "Beyoğlu",
      values: {
        cafes_density: { raw: 196.2, qualityFlag: "ok" },
        greenery: { raw: 2, qualityFlag: "estimated" },
      },
    },
    {
      neighborhoodId: 1049,
      name: "Asmalı Mescit Mahallesi",
      districtName: "Beyoğlu",
      values: {
        cafes_density: { raw: 383.4, qualityFlag: "ok" },
      },
    },
  ],
};

const CRITERIA: Criteria = {
  weights: [
    { metricKey: "cafes_density", weight: 1 },
    { metricKey: "greenery", weight: 1 },
  ],
};

function renderTable({
  criteria = CRITERIA,
  state = { status: "ready", data: DATA } as const,
  sort = null,
}: {
  criteria?: Criteria;
  state?: DataTableState;
  sort?: { key: string; direction: "ascending" | "descending" } | null;
} = {}): string {
  return renderToStaticMarkup(
    <DataTableView
      criteria={criteria}
      state={state}
      sort={sort}
      locale="fr"
      onRemoveCriterion={() => {}}
      onSortChange={() => {}}
      onShowOnMap={() => {}}
      onReload={() => {}}
    />,
  );
}

function bodyOf(markup: string): string {
  const start = markup.indexOf("<tbody");
  const end = markup.indexOf("</tbody>");
  return start === -1 || end === -1 ? "" : markup.slice(start, end);
}

describe("data view", () => {
  it("shows one sortable column per published metric, labelled by the API", () => {
    const markup = renderTable();

    expect(markup.match(/aria-sort="none"/g)).toHaveLength(2);
    expect(markup).toContain("Densité de cafés");
    expect(markup).toContain("Verdure");
  });

  it("repeats the unit next to every value and shows the observation date", () => {
    const markup = renderTable();
    const body = bodyOf(markup);

    expect(body.match(/cafés\/km²/g)).toHaveLength(3);
    expect(body.match(/% de la surface/g)).toHaveLength(2);
    expect(markup).toContain("2026-10-30");
    expect(markup).toContain("2021-12-31");
  });

  it("flags estimated values and leaves missing values explicit", () => {
    const markup = renderTable();

    expect(markup.match(/Estimation/g)).toHaveLength(2);
    expect(markup).toContain("Pas de donnée");
  });

  it("freezes the neighborhood column and keeps the map action on each row", () => {
    const markup = renderTable();

    expect(markup).toContain("sticky start-0");
    expect(markup).toContain('scope="row"');
    expect(markup.match(/scope="col"/g)).toHaveLength(5);
    expect(markup.match(/Voir sur la carte/g)).toHaveLength(3);
  });

  it("never highlights a row as the best one", () => {
    const markup = renderTable();
    const body = bodyOf(markup);

    expect(body).not.toContain("bg-highlight");
    expect(body).toContain("hover:bg-surface");
  });

  it("titles the table according to the shared ranking metric", () => {
    expect(renderTable()).toContain("Valeurs par quartier");
    expect(renderTable({ sort: { key: "greenery", direction: "descending" } })).toContain(
      "Classement des quartiers",
    );
  });

  it("numbers the rows and marks the first three only when the order rewards the best value", () => {
    const markup = renderTable({ sort: { key: "greenery", direction: "descending" } });
    const body = bodyOf(markup);

    expect(body).toMatch(/data-rank="1" data-merit="true"/);
    expect(body).toMatch(/data-rank="2" data-merit="true"/);
    expect(body).toMatch(/data-rank="3" data-merit="true"/);
    expect(body).toContain("bg-highlight/10");
  });

  it("keeps the rank numerals plain when the order does not put the best value first", () => {
    const markup = renderTable({ sort: { key: "greenery", direction: "ascending" } });
    const body = bodyOf(markup);

    expect(body).not.toContain('data-merit="true"');
    expect(body).toContain("bg-highlight/10");
  });

  it("ranks by the column chosen in the URL and keeps missing values last", () => {
    const markup = renderTable({
      sort: { key: "greenery", direction: "descending" },
    });
    const body = bodyOf(markup);

    expect(body.indexOf("Caferağa")).toBeLessThan(body.indexOf("Cihangir"));
    expect(body.indexOf("Cihangir")).toBeLessThan(body.indexOf("Asmalı Mescit"));
    expect(markup).toContain('aria-sort="descending"');
    expect(markup.match(/aria-sort="none"/g)).toHaveLength(1);
  });

  it("ignores a sort key that is not an active column", () => {
    const markup = renderTable({ sort: { key: "nightlife", direction: "descending" } });
    const body = bodyOf(markup);

    expect(markup).not.toContain('aria-sort="descending"');
    expect(body.indexOf("Asmalı Mescit")).toBeLessThan(body.indexOf("Caferağa"));
    expect(body.indexOf("Caferağa")).toBeLessThan(body.indexOf("Cihangir"));
  });

  it("shows the active criteria and names the ones the API does not publish", () => {
    const markup = renderTable({
      criteria: {
        weights: [
          { metricKey: "greenery", weight: 1 },
          { metricKey: "quiet", weight: 1 },
        ],
      },
    });

    expect(markup).toContain("Critères actifs");
    expect(markup).toContain("Retirer ce critère");
    expect(markup).toContain("Critères non publiés par l&#x27;API");
    expect(markup).toContain("quiet");
  });

  it("shows the source, licence, attribution and method of every column", () => {
    const markup = renderTable();

    expect(markup).toContain("Sources et dates");
    expect(markup).toContain("OpenStreetMap contributors / Geofabrik GmbH");
    expect(markup).toContain("© ESA WorldCover project 2021");
    expect(markup).toContain("ODbL 1.0");
    expect(markup).toContain("Part de la surface classée verte dans la tuile.");
    expect(markup).toContain("2026-10-31");
  });

  it("still shows every published metric when no criterion is active", () => {
    const markup = renderTable({ criteria: { weights: [] } });

    expect(markup).toContain("<table");
    expect(markup).toContain("Densité de cafés");
    expect(markup).toContain("Verdure");
    expect(markup).not.toContain("Aucun critère actif");
  });

  it("filters the table by neighborhood or district name", () => {
    const markup = renderTable({ state: { status: "ready", data: DATA } });

    expect(markup).toContain("Rechercher un quartier");
    expect(markup).toContain("Quartiers affichés");
  });

  it("shows fifty rows per page, numbers ranks across pages and counts the pages", () => {
    const manyRows = Array.from({ length: 120 }, (_, index) => ({
      neighborhoodId: index + 1,
      name: `Quartier ${index + 1}`,
      districtName: "Kadıköy",
      values: { greenery: { raw: index, qualityFlag: "ok" as const } },
    }));
    const markup = renderTable({ state: { status: "ready", data: { ...DATA, rows: manyRows } } });

    expect(markup.match(/data-rank="/g)).toHaveLength(50);
    expect(markup).toContain('data-rank="50"');
    expect(markup).not.toContain('data-rank="51"');
    expect(markup).toContain('aria-label="Pagination du tableau des quartiers"');
    expect(markup).toContain(">3</span>");
    expect(markup).toContain("Précédent");
    expect(markup).toContain("disabled");
    expect(markup).toContain("Suivant");
  });

  it("reports a loading state", () => {
    expect(renderTable({ state: { status: "loading" } })).toContain("Chargement des valeurs");
  });

  it("explains a failed request and offers a retry", () => {
    const markup = renderTable({ state: { status: "error", code: "database_unavailable" } });

    expect(markup).toContain("Les valeurs n&#x27;ont pas pu être chargées.");
    expect(markup).toContain("database_unavailable");
    expect(markup).toContain("Réessayer");
  });
});
