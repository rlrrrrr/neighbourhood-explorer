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

export function buildFillPaint(
  colors: ChoroplethColors,
): NonNullable<FillLayerSpecification["paint"]> {
  const fillColor: ExpressionSpecification = [
    "case",
    ["==", ["typeof", ["get", "normalized"]], "number"],
    [
      "interpolate",
      ["linear"],
      ["get", "normalized"],
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
    ],
    colors.noData,
  ];

  return {
    "fill-color": fillColor,
    "fill-opacity": 0.82,
  };
}

export function buildNoDataFillPaint(): NonNullable<FillLayerSpecification["paint"]> {
  return {
    "fill-pattern": NO_DATA_PATTERN_IMAGE_ID,
    "fill-opacity": 0.82,
  };
}

export function buildBorderPaint(
  colors: ChoroplethColors,
): NonNullable<LineLayerSpecification["paint"]> {
  return {
    "line-color": colors.border,
    "line-width": 0.7,
  };
}

export function buildSelectionPaint(
  colors: ChoroplethColors,
): NonNullable<LineLayerSpecification["paint"]> {
  return {
    "line-color": colors.text,
    "line-width": 2.4,
  };
}
