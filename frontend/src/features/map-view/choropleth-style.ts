import type {
  ExpressionSpecification,
  FillLayerSpecification,
  LineLayerSpecification,
} from "maplibre-gl";

export type ChoroplethColors = {
  ramp: [string, string, string, string, string];
  noData: string;
  border: string;
  text: string;
};

export const NO_DATA_PATTERN_IMAGE_ID = "no-data-hatch";

type StyleLayerSummary = {
  id: string;
  type: string;
  "source-layer"?: string;
};

export function findChoroplethAnchorLayerId(
  layers: readonly StyleLayerSummary[],
): string | undefined {
  const firstTransportLayer = layers.find((layer) => layer["source-layer"] === "transportation");

  return firstTransportLayer?.id ?? layers.find((layer) => layer.type === "symbol")?.id;
}

export function readChoroplethColors(root: HTMLElement): ChoroplethColors {
  const probe = root.ownerDocument.createElement("span");
  probe.setAttribute("aria-hidden", "true");
  probe.style.position = "absolute";
  probe.style.visibility = "hidden";
  probe.style.pointerEvents = "none";
  root.appendChild(probe);

  const read = (name: string) => {
    probe.style.color = `var(${name})`;

    return toMapLibreColor(getComputedStyle(probe).color);
  };

  try {
    return {
      ramp: [
        read("--ramp-1"),
        read("--ramp-2"),
        read("--ramp-3"),
        read("--ramp-4"),
        read("--ramp-5"),
      ],
      noData: read("--no-data"),
      border: read("--border"),
      text: read("--text"),
    };
  } finally {
    probe.remove();
  }
}

export function toMapLibreColor(value: string): string {
  const srgb = value.match(/^color\(srgb\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)(?:\s*\/\s*([\d.]+))?\)$/);

  if (srgb === null) {
    return value;
  }

  const [red, green, blue] = [srgb[1], srgb[2], srgb[3]].map((channel) =>
    Math.round(Number(channel) * 255),
  );

  const alpha = srgb[4] === undefined ? 1 : Number(srgb[4]);

  return `rgba(${red}, ${green}, ${blue}, ${alpha})`;
}

export function zoomInterpolation(
  stops: ReadonlyArray<readonly [zoom: number, value: number]>,
): ExpressionSpecification {
  return [
    "interpolate",
    ["linear"],
    ["zoom"],
    ...stops.flatMap(([zoom, value]) => [zoom, value] as const),
  ];
}

export function fillOpacityByZoom(): ExpressionSpecification {
  return zoomInterpolation([
    [8, 0.85],
    [12, 0.6],
    [15, 0.35],
    [16.5, 0.18],
  ]);
}

export function borderWidthByZoom(): ExpressionSpecification {
  return zoomInterpolation([
    [8, 0.6],
    [12, 1.2],
    [15, 2.4],
    [16.5, 3],
  ]);
}

export function selectionWidthByZoom(): ExpressionSpecification {
  return zoomInterpolation([
    [8, 2.4],
    [12, 3.2],
    [15, 4.4],
  ]);
}

export function buildFillPaint(
  colors: ChoroplethColors,
  blend: number | null = null,
): NonNullable<FillLayerSpecification["paint"]> {
  const rampColor = (property: string): ExpressionSpecification => [
    "interpolate",
    ["linear"],
    ["get", property],
    0,
    colors.ramp[0],
    0.25,
    colors.ramp[1],
    0.5,
    colors.ramp[2],
    0.75,
    colors.ramp[3],
    1,
    colors.ramp[4],
  ];

  const valueColor = (property: string): ExpressionSpecification => [
    "case",
    ["==", ["typeof", ["get", property]], "number"],
    rampColor(property),
    colors.noData,
  ];

  const fillColor: ExpressionSpecification =
    blend === null
      ? valueColor("normalized")
      : [
          "interpolate",
          ["linear"],
          blend,
          0,
          valueColor("previousNormalized"),
          1,
          valueColor("normalized"),
        ];

  return {
    "fill-color": fillColor,
    "fill-opacity": fillOpacityByZoom(),
  };
}

export function buildNoDataFillPaint(): NonNullable<FillLayerSpecification["paint"]> {
  return {
    "fill-pattern": NO_DATA_PATTERN_IMAGE_ID,
    "fill-opacity": zoomInterpolation([
      [8, 0.8],
      [12, 0.55],
      [15, 0.35],
      [16.5, 0.18],
    ]),
  };
}

export function buildBorderPaint(
  colors: ChoroplethColors,
): NonNullable<LineLayerSpecification["paint"]> {
  return {
    "line-color": colors.border,
    "line-width": borderWidthByZoom(),
  };
}

export function buildSelectionPaint(
  colors: ChoroplethColors,
): NonNullable<LineLayerSpecification["paint"]> {
  return {
    "line-color": colors.text,
    "line-width": selectionWidthByZoom(),
  };
}
