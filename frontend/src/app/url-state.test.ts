import { describe, expect, it } from "vite-plus/test";
import {
  searchWithNeighborhood,
  searchWithTableSort,
  tableSortFromSearch,
  validateSearch,
  viewForPath,
  viewPath,
  type UrlSearch,
  type ViewName,
} from "./url-state";

const VIEW_PATH_CASES: ReadonlyArray<[ViewName, string]> = [
  ["home", "/"],
  ["chat", "/chat"],
  ["map", "/map"],
  ["data", "/data"],
];

describe("view paths", () => {
  it("maps every view to a path and back", () => {
    for (const [view, path] of VIEW_PATH_CASES) {
      expect(viewPath(view)).toBe(path);
      expect(viewForPath(path)).toBe(view);
    }
  });

  it("falls back to the home view on an unknown path", () => {
    expect(viewForPath("/inconnu")).toBe("home");
    expect(viewForPath("/chat/")).toBe("home");
  });
});

describe("search validation", () => {
  it("keeps a neighborhood name and drops anything that is not a short string", () => {
    expect(validateSearch({ neighborhood: " Cihangir " })).toEqual({ neighborhood: "Cihangir" });
    expect(validateSearch({ neighborhood: "" })).toEqual({});
    expect(validateSearch({ neighborhood: "   " })).toEqual({});
    expect(validateSearch({ neighborhood: 42 })).toEqual({});
    expect(validateSearch({ neighborhood: ["Moda"] })).toEqual({});
    expect(validateSearch({ neighborhood: "x".repeat(81) })).toEqual({});
    expect(validateSearch({})).toEqual({});
  });

  it("keeps a column key and normalises the direction", () => {
    expect(validateSearch({ sort: "cafes" })).toEqual({ sort: "cafes", order: "asc" });
    expect(validateSearch({ sort: "cafes", order: "desc" })).toEqual({
      sort: "cafes",
      order: "desc",
    });
    expect(validateSearch({ sort: "cafes", order: "sideways" })).toEqual({
      sort: "cafes",
      order: "asc",
    });
  });

  it("ignores a column key that is not a slug", () => {
    expect(validateSearch({ sort: "Cafés" })).toEqual({});
    expect(validateSearch({ sort: "cafes;drop" })).toEqual({});
    expect(validateSearch({ sort: "1cafes" })).toEqual({});
    expect(validateSearch({ sort: "" })).toEqual({});
    expect(validateSearch({ sort: 3 })).toEqual({});
  });

  it("ignores the direction of an absent column", () => {
    expect(validateSearch({ order: "desc" })).toEqual({});
  });

  it("rebuilds the same state when it validates its own output", () => {
    const searches: UrlSearch[] = [
      {},
      { neighborhood: "Cihangir" },
      { sort: "green", order: "desc" },
      { neighborhood: "Moda", sort: "metro", order: "asc" },
    ];

    for (const search of searches) {
      expect(validateSearch({ ...search })).toEqual(search);
    }
  });
});

describe("table sort projection", () => {
  it("has no sort without a column", () => {
    expect(tableSortFromSearch({})).toBeNull();
    expect(tableSortFromSearch({ neighborhood: "Moda" })).toBeNull();
  });

  it("defaults to ascending", () => {
    expect(tableSortFromSearch({ sort: "cafes" })).toEqual({
      key: "cafes",
      direction: "ascending",
    });
    expect(tableSortFromSearch({ sort: "cafes", order: "desc" })).toEqual({
      key: "cafes",
      direction: "descending",
    });
  });

  it("survives a round trip through the search state", () => {
    for (const tableSort of [
      { key: "cafes", direction: "ascending" } as const,
      { key: "metro", direction: "descending" } as const,
    ]) {
      const search = searchWithTableSort({}, tableSort);
      expect(validateSearch({ ...search })).toEqual(search);
      expect(tableSortFromSearch(validateSearch({ ...search }))).toEqual(tableSort);
    }
  });

  it("drops the sort from the search when it is cleared", () => {
    const search = searchWithTableSort(
      { neighborhood: "Moda", sort: "cafes", order: "desc" },
      null,
    );

    expect(search).toEqual({ neighborhood: "Moda", sort: undefined, order: undefined });
    expect(validateSearch({ ...search })).toEqual({ neighborhood: "Moda" });
  });
});

describe("neighborhood projection", () => {
  it("keeps the other search entries", () => {
    expect(searchWithNeighborhood({ sort: "cafes", order: "desc" }, "Cihangir")).toEqual({
      sort: "cafes",
      order: "desc",
      neighborhood: "Cihangir",
    });
  });

  it("drops the neighborhood when it is cleared", () => {
    expect(searchWithNeighborhood({ neighborhood: "Moda" }, null)).toEqual({
      neighborhood: undefined,
    });
    expect(validateSearch({ neighborhood: undefined })).toEqual({});
  });
});
