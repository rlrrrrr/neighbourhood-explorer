import { describe, expect, it } from "vite-plus/test";
import {
  buildBorderPaint,
  buildFillPaint,
  buildNoDataFillPaint,
  buildSelectionPaint,
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
    expect(paint["fill-opacity"]).toBe(0.82);
  });

  it("fills missing values with the hatch image instead of a ramp colour", () => {
    expect(buildNoDataFillPaint()).toEqual({
      "fill-pattern": "no-data-hatch",
      "fill-opacity": 0.82,
    });
  });

  it("draws borders and selection with the matching role tokens", () => {
    expect(buildBorderPaint(colors)).toEqual({
      "line-color": "#000000",
      "line-width": 0.7,
    });
    expect(buildSelectionPaint(colors)).toEqual({
      "line-color": "#111111",
      "line-width": 2.4,
    });
  });

  it("converts CSS color-mix output to a MapLibre-compatible color", () => {
    expect(toMapLibreColor("color(srgb 0.5 0.5 0.5)")).toBe("rgba(128, 128, 128, 1)");
    expect(toMapLibreColor("color(srgb 0.2 0.4 0.6 / 0.5)")).toBe("rgba(51, 102, 153, 0.5)");
    expect(toMapLibreColor("rgb(1, 2, 3)")).toBe("rgb(1, 2, 3)");
    expect(toMapLibreColor("#ff0000")).toBe("#ff0000");
  });
});
