import { describe, expect, it } from "vite-plus/test";
import {
  buildBorderPaint,
  buildFillPaint,
  buildNoDataFillPaint,
  buildSelectionPaint,
  findChoroplethAnchorLayerId,
  toMapLibreColor,
  type ChoroplethColors,
} from "./choropleth-style";

const colors: ChoroplethColors = {
  ramp: ["#111111", "#222222", "#333333", "#444444", "#555555"],
  noData: "#999999",
  border: "#000000",
  text: "#111111",
};

describe("choropleth paint", () => {
  it("builds a five-step ramp with a distinct missing-value colour", () => {
    const paint = buildFillPaint(colors);

    expect(paint["fill-color"]).toEqual([
      "case",
      ["==", ["typeof", ["get", "normalized"]], "number"],
      [
        "interpolate",
        ["linear"],
        ["get", "normalized"],
        0,
        "#111111",
        0.25,
        "#222222",
        0.5,
        "#333333",
        0.75,
        "#444444",
        1,
        "#555555",
      ],
      "#999999",
    ]);
    expect(paint["fill-opacity"]).toEqual([
      "interpolate",
      ["linear"],
      ["zoom"],
      8,
      0.85,
      12,
      0.6,
      15,
      0.35,
      16.5,
      0.18,
    ]);
  });

  it("blends the previous and current ramps while a new metric settles", () => {
    const ramp = (property: string) => [
      "interpolate",
      ["linear"],
      ["get", property],
      0,
      "#111111",
      0.25,
      "#222222",
      0.5,
      "#333333",
      0.75,
      "#444444",
      1,
      "#555555",
    ];

    const valueColor = (property: string) => [
      "case",
      ["==", ["typeof", ["get", property]], "number"],
      ramp(property),
      "#999999",
    ];

    expect(buildFillPaint(colors, 0.4)["fill-color"]).toEqual([
      "interpolate",
      ["linear"],
      0.4,
      0,
      valueColor("previousNormalized"),
      1,
      valueColor("normalized"),
    ]);
  });

  it("fills missing values with the hatch image instead of a ramp colour", () => {
    expect(buildNoDataFillPaint()).toEqual({
      "fill-pattern": "no-data-hatch",
      "fill-opacity": ["interpolate", ["linear"], ["zoom"], 8, 0.8, 12, 0.55, 15, 0.35, 16.5, 0.18],
    });
  });

  it("draws borders and selection with the matching role tokens", () => {
    expect(buildBorderPaint(colors)).toEqual({
      "line-color": "#000000",
      "line-width": ["interpolate", ["linear"], ["zoom"], 8, 0.6, 12, 1.2, 15, 2.4, 16.5, 3],
    });
    expect(buildSelectionPaint(colors)).toEqual({
      "line-color": "#111111",
      "line-width": ["interpolate", ["linear"], ["zoom"], 8, 2.4, 12, 3.2, 15, 4.4],
    });
  });

  it("converts CSS color-mix output to a MapLibre-compatible color", () => {
    expect(toMapLibreColor("color(srgb 0.5 0.5 0.5)")).toBe("rgba(128, 128, 128, 1)");
    expect(toMapLibreColor("color(srgb 0.2 0.4 0.6 / 0.5)")).toBe("rgba(51, 102, 153, 0.5)");
    expect(toMapLibreColor("rgb(1, 2, 3)")).toBe("rgb(1, 2, 3)");
    expect(toMapLibreColor("#ff0000")).toBe("#ff0000");
  });
});

describe("choropleth anchor layer", () => {
  it("sits below the first road layer of the light basemap", () => {
    const anchor = findChoroplethAnchorLayerId([
      { id: "background", type: "background" },
      { id: "park", type: "fill", "source-layer": "park" },
      { id: "water", type: "fill", "source-layer": "water" },
      { id: "building", type: "fill", "source-layer": "building" },
      { id: "tunnel_motorway_casing", type: "line", "source-layer": "transportation" },
      { id: "highway_minor", type: "line", "source-layer": "transportation" },
      { id: "waterway_line_label", type: "symbol", "source-layer": "waterway" },
    ]);

    expect(anchor).toBe("tunnel_motorway_casing");
  });

  it("prefers the road layer even when a label comes earlier in the dark basemap", () => {
    const anchor = findChoroplethAnchorLayerId([
      { id: "background", type: "background" },
      { id: "water", type: "fill", "source-layer": "water" },
      { id: "water_name", type: "symbol", "source-layer": "water_name" },
      { id: "building", type: "fill", "source-layer": "building" },
      { id: "aeroway-taxiway", type: "line", "source-layer": "transportation" },
      { id: "highway_path", type: "line", "source-layer": "transportation" },
    ]);

    expect(anchor).toBe("aeroway-taxiway");
  });

  it("falls back to the first label when the style has no road layer", () => {
    const anchor = findChoroplethAnchorLayerId([
      { id: "background", type: "background" },
      { id: "water", type: "fill", "source-layer": "water" },
      { id: "water_name", type: "symbol", "source-layer": "water_name" },
    ]);

    expect(anchor).toBe("water_name");
  });

  it("returns no anchor when the style has no foreground layer", () => {
    const anchor = findChoroplethAnchorLayerId([
      { id: "background", type: "background" },
      { id: "water", type: "fill", "source-layer": "water" },
    ]);

    expect(anchor).toBeUndefined();
  });
});
