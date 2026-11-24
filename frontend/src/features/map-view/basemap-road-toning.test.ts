import { describe, expect, it } from "vite-plus/test";
import { findTonedRoadLayerIds } from "./basemap-road-toning";

describe("basemap road toning", () => {
  it("targets motorway and major road lines only", () => {
    const layerIds = findTonedRoadLayerIds([
      { id: "background", type: "background" },
      { id: "building", type: "fill" },
      { id: "highway_minor", type: "line" },
      { id: "highway_path", type: "line" },
      { id: "highway_major_casing", type: "line" },
      { id: "highway_motorway_inner", type: "line" },
      { id: "tunnel_motorway_casing", type: "line" },
      { id: "highway-name-major", type: "symbol" },
      { id: "label_city", type: "symbol" },
    ]);

    expect(layerIds).toEqual([
      "highway_major_casing",
      "highway_motorway_inner",
      "tunnel_motorway_casing",
    ]);
  });
});
