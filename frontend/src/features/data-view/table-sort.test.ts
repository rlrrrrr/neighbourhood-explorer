import { describe, expect, it } from "vite-plus/test";
import { nextSort, sortRows, type TableSort } from "./table-sort";

interface Row {
  name: string;
  cafes: number;
}

const ROWS: readonly Row[] = [
  { name: "Moda", cafes: 24.1 },
  { name: "Cihangir", cafes: 18.6 },
  { name: "Yeldeğirmeni", cafes: 15.2 },
  { name: "Kuzguncuk", cafes: 18.6 },
];

const sortValueOf = (row: Row, key: string) => (key === "cafes" ? row.cafes : 0);

describe("table sort", () => {
  it("starts ascending on a new column and flips on the same column", () => {
    expect(nextSort(null, "cafes")).toEqual({ key: "cafes", direction: "ascending" });
    expect(nextSort({ key: "cafes", direction: "ascending" }, "cafes")).toEqual({
      key: "cafes",
      direction: "descending",
    });
    expect(nextSort({ key: "cafes", direction: "descending" }, "green")).toEqual({
      key: "green",
      direction: "ascending",
    });
  });

  it("keeps every row and the source order when no column is sorted", () => {
    const sorted = sortRows({ rows: ROWS, sort: null, sortValueOf });

    expect(sorted).toEqual([...ROWS]);
  });

  it("sorts ascending and descending without losing a row", () => {
    const ascending = sortRows({
      rows: ROWS,
      sort: { key: "cafes", direction: "ascending" },
      sortValueOf,
    });
    const descending = sortRows({
      rows: ROWS,
      sort: { key: "cafes", direction: "descending" },
      sortValueOf,
    });

    expect(ascending.map((row) => row.name)).toEqual([
      "Yeldeğirmeni",
      "Cihangir",
      "Kuzguncuk",
      "Moda",
    ]);
    expect(descending.map((row) => row.name)).toEqual([
      "Moda",
      "Cihangir",
      "Kuzguncuk",
      "Yeldeğirmeni",
    ]);
  });

  it("keeps the source order between equal values", () => {
    const sort: TableSort = { key: "cafes", direction: "descending" };
    const sorted = sortRows({ rows: ROWS, sort, sortValueOf });

    expect(sorted.filter((row) => row.cafes === 18.6).map((row) => row.name)).toEqual([
      "Cihangir",
      "Kuzguncuk",
    ]);
  });

  it("does not mutate the source rows", () => {
    sortRows({ rows: ROWS, sort: { key: "cafes", direction: "descending" }, sortValueOf });

    expect(ROWS.map((row) => row.name)).toEqual(["Moda", "Cihangir", "Yeldeğirmeni", "Kuzguncuk"]);
  });
});
