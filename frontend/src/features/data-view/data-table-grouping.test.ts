import { describe, expect, it } from "vite-plus/test";
import type { DataTableRow } from "./data-table-client";
import { districtGroups, orderGroupedRows, visibleMetricKeys } from "./data-table-grouping";

const ROWS: DataTableRow[] = [
  {
    neighborhoodId: 1,
    name: "Z",
    districtName: "Beyoğlu",
    values: { greenery: { raw: 20, qualityFlag: "ok" } },
  },
  {
    neighborhoodId: 2,
    name: "A",
    districtName: "Kadıköy",
    values: { greenery: { raw: 60, qualityFlag: "ok" } },
  },
  {
    neighborhoodId: 3,
    name: "B",
    districtName: "Beyoğlu",
    values: { greenery: { raw: 40, qualityFlag: "ok" } },
  },
  {
    neighborhoodId: 4,
    name: "C",
    districtName: "Kadıköy",
    values: {},
  },
];

describe("data table grouping", () => {
  it("orders rows by district, then by the chosen metric with missing values last", () => {
    const ordered = orderGroupedRows({
      rows: ROWS,
      sort: { key: "greenery", direction: "descending" },
      locale: "fr",
    });

    expect(ordered.map((row) => row.neighborhoodId)).toEqual([3, 1, 2, 4]);
  });

  it("falls back to the localized name when no metric is sorted", () => {
    const ordered = orderGroupedRows({ rows: ROWS, sort: null, locale: "fr" });

    expect(ordered.map((row) => row.neighborhoodId)).toEqual([3, 1, 2, 4]);
  });

  it("computes the count and average of measured values per district", () => {
    const groups = districtGroups({ rows: ROWS, metricKey: "greenery" });

    expect(groups).toEqual([
      { districtName: "Beyoğlu", count: 2, average: 30, maximum: 40 },
      { districtName: "Kadıköy", count: 2, average: 60, maximum: 60 },
    ]);
  });

  it("keeps two compact columns and adds the sorted metric", () => {
    const metrics = [
      { key: "cafes_density" },
      { key: "greenery" },
      { key: "transport_accessibility" },
    ];

    expect(visibleMetricKeys({ metrics, sortedMetricKey: undefined })).toEqual([
      "greenery",
      "transport_accessibility",
    ]);
    expect(visibleMetricKeys({ metrics, sortedMetricKey: "cafes_density" })).toEqual([
      "greenery",
      "transport_accessibility",
      "cafes_density",
    ]);
  });

  it("reports a missing average when no row of the district has a value", () => {
    const groups = districtGroups({
      rows: [ROWS[3]!],
      metricKey: "greenery",
    });

    expect(groups[0]?.average).toBeNull();
  });
});
