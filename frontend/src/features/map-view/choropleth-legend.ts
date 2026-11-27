import { hasMetricValue, type ChoroplethFeatureCollection } from "./choropleth";

export const LEGEND_SWATCH_CLASSES = [
  "bg-ramp-1",
  "bg-ramp-2",
  "bg-ramp-3",
  "bg-ramp-4",
  "bg-ramp-5",
] as const;

const NORMALIZED_STOPS = [0, 0.25, 0.5, 0.75, 1] as const;

type MeasuredValue = { normalized: number; raw: number };

export function buildLegendStops(
  collection: ChoroplethFeatureCollection,
): ReadonlyArray<number | null> {
  const values = collection.features
    .map((feature) => feature.properties)
    .filter(hasMetricValue)
    .map((properties): MeasuredValue => ({
      normalized: properties.normalized,
      raw: properties.raw,
    }));

  if (values.length === 0) {
    return LEGEND_SWATCH_CLASSES.map(() => null);
  }

  return NORMALIZED_STOPS.map((stop) => nearestRawValue(values, stop));
}

function nearestRawValue(values: readonly MeasuredValue[], stop: number): number {
  return values.reduce((closest, candidate) => {
    const candidateDistance = Math.abs(candidate.normalized - stop);
    const closestDistance = Math.abs(closest.normalized - stop);

    if (candidateDistance < closestDistance) {
      return candidate;
    }

    if (candidateDistance === closestDistance && candidate.raw < closest.raw) {
      return candidate;
    }

    return closest;
  }).raw;
}
