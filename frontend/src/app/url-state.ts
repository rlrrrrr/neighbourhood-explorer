import type { TableSort } from "../features/data-view/table-sort";

export type ViewName = "home" | "chat" | "map" | "data";

export type UrlSearch = {
  neighborhood?: string;
  sort?: string;
  order?: "asc" | "desc";
};

const VIEW_PATHS = {
  home: "/",
  chat: "/chat",
  map: "/map",
  data: "/data",
} as const satisfies Record<ViewName, string>;

export type ViewPath = (typeof VIEW_PATHS)[ViewName];

const MAX_NEIGHBORHOOD_LENGTH = 80;
const MAX_SORT_KEY_LENGTH = 32;
const SORT_KEY_PATTERN = /^[a-z][a-z0-9-]*$/;

function readBoundedText(value: unknown, maxLength: number): string | undefined {
  if (typeof value !== "string") {
    return undefined;
  }
  const text = value.trim();
  return text.length === 0 || text.length > maxLength ? undefined : text;
}

export function viewPath(view: ViewName): ViewPath {
  return VIEW_PATHS[view];
}

export function viewForPath(pathname: string): ViewName {
  const entry = (Object.entries(VIEW_PATHS) as Array<[ViewName, ViewPath]>).find(
    ([, path]) => path === pathname,
  );
  return entry ? entry[0] : "home";
}

export function validateSearch(search: Record<string, unknown>): UrlSearch {
  const result: UrlSearch = {};
  const neighborhood = readBoundedText(search.neighborhood, MAX_NEIGHBORHOOD_LENGTH);
  if (neighborhood) {
    result.neighborhood = neighborhood;
  }
  const sort = readBoundedText(search.sort, MAX_SORT_KEY_LENGTH);
  if (sort && SORT_KEY_PATTERN.test(sort)) {
    result.sort = sort;
    result.order = search.order === "desc" ? "desc" : "asc";
  }
  return result;
}

export function tableSortFromSearch(search: UrlSearch): TableSort | null {
  if (!search.sort) {
    return null;
  }
  return { key: search.sort, direction: search.order === "desc" ? "descending" : "ascending" };
}

export function searchWithTableSort(search: UrlSearch, tableSort: TableSort | null): UrlSearch {
  return {
    ...search,
    sort: tableSort?.key,
    order: tableSort ? (tableSort.direction === "descending" ? "desc" : "asc") : undefined,
  };
}

export function searchWithNeighborhood(search: UrlSearch, neighborhood: string | null): UrlSearch {
  return { ...search, neighborhood: neighborhood ?? undefined };
}
