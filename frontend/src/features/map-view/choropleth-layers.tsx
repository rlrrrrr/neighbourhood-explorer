import { useEffect, useMemo, useRef, useState } from "react";
import { MapGeoJSON, useMap } from "../../components/ui/map";
import { mapCameraAnimationDuration } from "./map-camera-duration";
import {
  collectionBounds,
  splitFeaturesByMetricValue,
  type ChoroplethFeatureCollection,
  type ChoroplethProperties,
} from "./choropleth";
import {
  buildBorderPaint,
  buildFillPaint,
  buildNoDataFillPaint,
  buildSelectionPaint,
  NO_DATA_PATTERN_IMAGE_ID,
  type ChoroplethColors,
} from "./choropleth-style";

type InsertionPoint = { ready: boolean; beforeId?: string };

function createNoDataPattern(backgroundColor: string, stripeColor: string): ImageData | null {
  const size = 8;
  const canvas = document.createElement("canvas");
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext("2d");
  if (context === null) {
    return null;
  }
  context.fillStyle = backgroundColor;
  context.fillRect(0, 0, size, size);
  context.globalAlpha = 0.35;
  context.strokeStyle = stripeColor;
  context.lineWidth = 1;
  context.beginPath();
  context.moveTo(-1, size + 1);
  context.lineTo(size + 1, -1);
  context.stroke();
  return context.getImageData(0, 0, size, size);
}

export function ChoroplethLayers({
  collection,
  selectedCollection,
  colors,
  onSelectNeighborhood,
}: {
  collection: ChoroplethFeatureCollection;
  selectedCollection: ChoroplethFeatureCollection | null;
  colors: ChoroplethColors;
  onSelectNeighborhood: (neighborhoodId: number) => void;
}) {
  const { map, isLoaded } = useMap();
  const [insertionPoint, setInsertionPoint] = useState<InsertionPoint>({ ready: false });
  const fittedSelectionRef = useRef<string | null>(null);
  const hasCompletedInitialFitRef = useRef(false);
  const { withValue, withoutValue } = useMemo(
    () => splitFeaturesByMetricValue(collection),
    [collection],
  );

  useEffect(() => {
    if (!map || !isLoaded) {
      setInsertionPoint({ ready: false });
      return;
    }

    const hatch = createNoDataPattern(colors.noData, colors.text);
    if (hatch !== null) {
      try {
        if (map.hasImage(NO_DATA_PATTERN_IMAGE_ID)) {
          map.updateImage(NO_DATA_PATTERN_IMAGE_ID, hatch);
        } else {
          map.addImage(NO_DATA_PATTERN_IMAGE_ID, hatch);
        }
      } catch {
        // The style can be mid-reload; the next style.load recreates the image.
      }
    }

    const firstSymbolLayer = map.getStyle().layers.find((layer) => layer.type === "symbol");
    setInsertionPoint({ ready: true, beforeId: firstSymbolLayer?.id });
  }, [map, isLoaded, colors.noData, colors.text]);

  const hasSelection = selectedCollection !== null && selectedCollection.features.length > 0;
  const selectionKey = hasSelection
    ? `selection:${selectedCollection.features.map((feature) => feature.properties.id).join(",")}`
    : "all";
  useEffect(() => {
    if (!map || !isLoaded || fittedSelectionRef.current === selectionKey) {
      return;
    }
    const bounds = collectionBounds(hasSelection ? selectedCollection : collection);
    if (bounds === null) {
      return;
    }
    const isInitialFit = !hasCompletedInitialFitRef.current;
    const animateCamera = hasSelection || !isInitialFit;
    fittedSelectionRef.current = selectionKey;
    hasCompletedInitialFitRef.current = true;
    map.fitBounds(bounds, {
      padding: hasSelection ? 96 : 32,
      maxZoom: hasSelection ? 14 : 11,
      duration: animateCamera ? mapCameraAnimationDuration() : 0,
    });
  }, [map, isLoaded, collection, selectedCollection, hasSelection, selectionKey]);

  if (!insertionPoint.ready) {
    return null;
  }

  return (
    <>
      <MapGeoJSON<ChoroplethProperties>
        id="population-density"
        data={withValue}
        promoteId="id"
        beforeId={insertionPoint.beforeId}
        fillPaint={buildFillPaint(colors)}
        linePaint={buildBorderPaint(colors)}
        interactive
        onClick={(event) => onSelectNeighborhood(event.feature.properties.id)}
      />
      {withoutValue.features.length > 0 ? (
        <MapGeoJSON<ChoroplethProperties>
          id="population-density-no-data"
          data={withoutValue}
          promoteId="id"
          beforeId={insertionPoint.beforeId}
          fillPaint={buildNoDataFillPaint()}
          linePaint={buildBorderPaint(colors)}
          interactive
          onClick={(event) => onSelectNeighborhood(event.feature.properties.id)}
        />
      ) : null}
      {selectedCollection !== null && selectedCollection.features.length > 0 ? (
        <MapGeoJSON<ChoroplethProperties>
          id="selected-neighborhood"
          data={selectedCollection}
          beforeId={insertionPoint.beforeId}
          fillPaint={false}
          linePaint={buildSelectionPaint(colors)}
        />
      ) : null}
    </>
  );
}
