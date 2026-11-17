import { describe, expect, it } from "vite-plus/test";
import type { DataTableRow } from "./data-table-client";
import { dominantQualityFlags } from "./data-table-quality";

function row({
  neighborhoodId,
  qualityFlags,
}: {
  neighborhoodId: number;
  qualityFlags: Record<string, "ok" | "estimated" | "inherited" | "stale" | "low_coverage">;
}): DataTableRow {
  return {
    neighborhoodId,
    name: `Quartier ${neighborhoodId}`,
    districtName: "Kadıköy",
    values: Object.fromEntries(
      Object.entries(qualityFlags).map(([metricKey, qualityFlag]) => [
        metricKey,
        { raw: neighborhoodId, qualityFlag },
      ]),
    ),
  };
}

describe("dominant quality flags", () => {
  it("reports the flag shared by most values of a metric", () => {
    const rows = [
      row({ neighborhoodId: 1, qualityFlags: { greenery: "estimated", cafes_density: "ok" } }),
      row({ neighborhoodId: 2, qualityFlags: { greenery: "estimated", cafes_density: "ok" } }),
      row({ neighborhoodId: 3, qualityFlags: { greenery: "inherited", cafes_density: "ok" } }),
    ];

    const flags = dominantQualityFlags({
      rows,
      metricKeys: ["greenery", "cafes_density"],
    });

    expect(flags.get("greenery")).toBe("estimated");
    expect(flags.get("cafes_density")).toBe("ok");
  });

  it("ignores metrics without any value", () => {
    const flags = dominantQualityFlags({
      rows: [row({ neighborhoodId: 1, qualityFlags: { greenery: "estimated" } })],
      metricKeys: ["greenery", "nightlife"],
    });

    expect(flags.has("greenery")).toBe(true);
    expect(flags.has("nightlife")).toBe(false);
  });
});
