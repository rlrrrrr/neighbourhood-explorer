import type { ExpressionSpecification, LayerSpecification } from "maplibre-gl";
import { zoomInterpolation } from "./choropleth-style";

type RoadLayerSummary = Pick<LayerSpecification, "id" | "type">;

const TONED_ROAD_LAYER_PATTERN = /(motorway|major|trunk|primary)/;

export function buildRoadOpacityByZoom(): ExpressionSpecification {
  return zoomInterpolation([
    [8, 0.15],
    [10.5, 0.5],
    [12, 1],
  ]);
}

export function findTonedRoadLayerIds(layers: readonly RoadLayerSummary[]): readonly string[] {
  return layers.flatMap((layer) =>
    layer.type === "line" && TONED_ROAD_LAYER_PATTERN.test(layer.id) ? [layer.id] : [],
  );
}
