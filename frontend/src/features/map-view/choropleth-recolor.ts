import { useEffect, useRef, useState } from "react";
import type { Feature, FeatureCollection, MultiPolygon, Polygon } from "geojson";
import {
  hasMetricValue,
  type ChoroplethFeatureCollection,
  type ChoroplethProperties,
} from "./choropleth";

export const CHOROPLETH_RECOLOR_MS = 220;

const EASE_OUT_EXPONENT = 4;

export type BlendedChoroplethProperties = ChoroplethProperties & {
  previousNormalized: number | null;
};

export type BlendedChoroplethFeatureCollection = FeatureCollection<
  Polygon | MultiPolygon,
  BlendedChoroplethProperties
>;

export type RecoloredCollection = {
  collection: ChoroplethFeatureCollection;
  blend: number | null;
};

export function choroplethRecolorDuration(): number {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : CHOROPLETH_RECOLOR_MS;
}

export function easeOutProgress(progress: number): number {
  return 1 - (1 - progress) ** EASE_OUT_EXPONENT;
}

export function withPreviousNormalized({
  previous,
  next,
}: {
  previous: ChoroplethFeatureCollection;
  next: ChoroplethFeatureCollection;
}): BlendedChoroplethFeatureCollection {
  const normalizedByID = new Map<number, number>();

  for (const feature of previous.features) {
    if (hasMetricValue(feature.properties)) {
      normalizedByID.set(feature.properties.id, feature.properties.normalized);
    }
  }

  const features: Array<Feature<Polygon | MultiPolygon, BlendedChoroplethProperties>> =
    next.features.map((feature) => {
      const properties = feature.properties;

      return {
        type: "Feature",
        geometry: feature.geometry,
        properties: {
          id: properties.id,
          name: properties.name,
          districtName: properties.districtName,
          raw: properties.raw,
          normalized: properties.normalized,
          qualityFlag: properties.qualityFlag,
          previousNormalized: normalizedByID.get(properties.id) ?? null,
        },
      };
    });

  return { type: "FeatureCollection", features };
}

export function interpolateChoroplethNormalized({
  from,
  to,
  progress,
}: {
  from: ChoroplethFeatureCollection;
  to: ChoroplethFeatureCollection;
  progress: number;
}): ChoroplethFeatureCollection {
  const normalizedByID = new Map<number, number>();

  for (const feature of from.features) {
    if (hasMetricValue(feature.properties)) {
      normalizedByID.set(feature.properties.id, feature.properties.normalized);
    }
  }

  const features: Array<Feature<Polygon | MultiPolygon, ChoroplethProperties>> = to.features.map(
    (feature) => {
      const properties = feature.properties;
      const origin = normalizedByID.get(properties.id);

      if (!hasMetricValue(properties) || origin === undefined) {
        return feature;
      }

      return {
        type: "Feature",
        geometry: feature.geometry,
        properties: {
          id: properties.id,
          name: properties.name,
          districtName: properties.districtName,
          raw: properties.raw,
          normalized: origin + (properties.normalized - origin) * progress,
          qualityFlag: properties.qualityFlag,
        },
      };
    },
  );

  return { type: "FeatureCollection", features };
}

export function useRecoloredCollection(
  collection: ChoroplethFeatureCollection,
  metricKey: string,
): RecoloredCollection {
  const [recolored, setRecolored] = useState<RecoloredCollection>({
    collection,
    blend: null,
  });

  const settledRef = useRef(collection);
  const targetRef = useRef(collection);
  const blendFromRef = useRef(collection);
  const blendValueRef = useRef<number | null>(null);
  const lastCollectionRef = useRef(collection);
  const lastMetricRef = useRef(metricKey);
  const pendingRecolorRef = useRef(false);
  const frameRef = useRef<number | null>(null);

  useEffect(() => {
    if (lastMetricRef.current !== metricKey) {
      pendingRecolorRef.current = true;
      lastMetricRef.current = metricKey;
    }

    if (lastCollectionRef.current === collection) {
      return;
    }

    lastCollectionRef.current = collection;

    if (frameRef.current !== null) {
      cancelAnimationFrame(frameRef.current);
      frameRef.current = null;
    }

    const duration = choroplethRecolorDuration();

    if (!pendingRecolorRef.current || duration === 0) {
      pendingRecolorRef.current = false;
      settledRef.current = collection;
      targetRef.current = collection;
      blendFromRef.current = collection;
      blendValueRef.current = null;
      setRecolored({ collection, blend: null });

      return;
    }

    pendingRecolorRef.current = false;

    const blendValue = blendValueRef.current;

    const origin =
      blendValue === null
        ? settledRef.current
        : interpolateChoroplethNormalized({
            from: blendFromRef.current,
            to: targetRef.current,
            progress: blendValue,
          });

    settledRef.current = targetRef.current;
    targetRef.current = collection;
    blendFromRef.current = origin;

    const blended = withPreviousNormalized({ previous: origin, next: collection });
    const startedAt = performance.now();

    const step = (now: number) => {
      const linearProgress = Math.max(0, Math.min(1, (now - startedAt) / duration));
      const blend = easeOutProgress(linearProgress);

      blendValueRef.current = blend;
      setRecolored({ collection: blended, blend });

      if (linearProgress < 1) {
        frameRef.current = requestAnimationFrame(step);
      } else {
        frameRef.current = null;
        blendValueRef.current = null;
        setRecolored({ collection, blend: null });
      }
    };

    frameRef.current = requestAnimationFrame(step);
  }, [collection, metricKey]);

  useEffect(
    () => () => {
      if (frameRef.current !== null) {
        cancelAnimationFrame(frameRef.current);
      }
    },
    [],
  );

  return recolored;
}
