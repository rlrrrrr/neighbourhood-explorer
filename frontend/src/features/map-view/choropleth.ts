import type { Feature, FeatureCollection, MultiPolygon, Polygon, Position } from "geojson";
import type { components } from "../../generated/api";

export const POPULATION_DENSITY_METRIC_KEY = "population_density";

export type ChoroplethProperties = {
  id: number;
  name: string;
  districtName: string;
  raw: number | null;
  normalized: number | null;
  qualityFlag: components["schemas"]["MetricValue"]["qualityFlag"] | null;
};

export type ChoroplethFeatureCollection = FeatureCollection<
  Polygon | MultiPolygon,
  ChoroplethProperties
>;

type NeighborhoodCollection = components["schemas"]["NeighborhoodFeatureCollection"];
type GeoJsonGeometry = components["schemas"]["GeoJsonGeometry"];
type MetricValues = components["schemas"]["MetricValues"];

export function buildChoroplethFeatureCollection({
  neighborhoods,
  metricValues,
  metricKey,
}: {
  neighborhoods: NeighborhoodCollection;
  metricValues: MetricValues;
  metricKey: string;
}): ChoroplethFeatureCollection {
  const values = metricValues[metricKey] ?? {};
  const features: Array<Feature<Polygon | MultiPolygon, ChoroplethProperties>> = [];

  for (const feature of neighborhoods.features) {
    const geometry = readGeometry(feature.geometry);
    if (geometry === null) {
      continue;
    }

    const value = values[String(feature.properties.id)];
    features.push({
      type: "Feature",
      geometry,
      properties: {
        id: feature.properties.id,
        name: feature.properties.name,
        districtName: feature.properties.districtName,
        raw: value?.raw ?? null,
        normalized: value?.normalized ?? null,
        qualityFlag: value?.qualityFlag ?? null,
      },
    });
  }

  return { type: "FeatureCollection", features };
}

export function selectFeaturesById(
  collection: ChoroplethFeatureCollection,
  neighborhoodId: number | null,
): ChoroplethFeatureCollection {
  if (neighborhoodId === null) {
    return { type: "FeatureCollection", features: [] };
  }

  return {
    type: "FeatureCollection",
    features: collection.features.filter((feature) => feature.properties.id === neighborhoodId),
  };
}

export function splitFeaturesByMetricValue(collection: ChoroplethFeatureCollection): {
  withValue: ChoroplethFeatureCollection;
  withoutValue: ChoroplethFeatureCollection;
} {
  const withValue: ChoroplethFeatureCollection["features"] = [];
  const withoutValue: ChoroplethFeatureCollection["features"] = [];

  for (const feature of collection.features) {
    if (typeof feature.properties.normalized === "number") {
      withValue.push(feature);
    } else {
      withoutValue.push(feature);
    }
  }

  return {
    withValue: { type: "FeatureCollection", features: withValue },
    withoutValue: { type: "FeatureCollection", features: withoutValue },
  };
}

export type MapBounds = [[number, number], [number, number]];

export function collectionBounds(collection: ChoroplethFeatureCollection): MapBounds | null {
  let minimumLongitude = Infinity;
  let minimumLatitude = Infinity;
  let maximumLongitude = -Infinity;
  let maximumLatitude = -Infinity;

  const include = (position: Position) => {
    minimumLongitude = Math.min(minimumLongitude, position[0]);
    minimumLatitude = Math.min(minimumLatitude, position[1]);
    maximumLongitude = Math.max(maximumLongitude, position[0]);
    maximumLatitude = Math.max(maximumLatitude, position[1]);
  };

  for (const feature of collection.features) {
    const polygons =
      feature.geometry.type === "Polygon"
        ? [feature.geometry.coordinates]
        : feature.geometry.coordinates;
    for (const polygon of polygons) {
      for (const ring of polygon) {
        for (const position of ring) {
          include(position);
        }
      }
    }
  }

  if (
    !Number.isFinite(minimumLongitude) ||
    !Number.isFinite(minimumLatitude) ||
    !Number.isFinite(maximumLongitude) ||
    !Number.isFinite(maximumLatitude)
  ) {
    return null;
  }

  return [
    [minimumLongitude, minimumLatitude],
    [maximumLongitude, maximumLatitude],
  ];
}

function readGeometry(geometry: GeoJsonGeometry): Polygon | MultiPolygon | null {
  switch (geometry.type) {
    case "Polygon":
      return isPolygonCoordinates(geometry.coordinates)
        ? { type: "Polygon", coordinates: geometry.coordinates }
        : null;
    case "MultiPolygon":
      return isMultiPolygonCoordinates(geometry.coordinates)
        ? { type: "MultiPolygon", coordinates: geometry.coordinates }
        : null;
    default:
      return null;
  }
}

function isMultiPolygonCoordinates(value: unknown): value is Position[][][] {
  return Array.isArray(value) && value.length > 0 && value.every(isPolygonCoordinates);
}

function isPolygonCoordinates(value: unknown): value is Position[][] {
  return Array.isArray(value) && value.length > 0 && value.every(isRing);
}

function isRing(value: unknown): value is Position[] {
  return Array.isArray(value) && value.length >= 4 && value.every(isPosition);
}

function isPosition(value: unknown): value is Position {
  return (
    Array.isArray(value) &&
    value.length >= 2 &&
    value.length <= 3 &&
    value.every((coordinate) => typeof coordinate === "number" && Number.isFinite(coordinate))
  );
}
