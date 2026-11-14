import type { components } from "../../generated/api";

export type MetricDefinition = components["schemas"]["MetricDefinition"];
export type MetricValue = components["schemas"]["MetricValue"];
export type MetricValues = components["schemas"]["MetricValues"];
export type MetricsResponse = components["schemas"]["MetricsResponse"];
export type NeighborhoodCollection = components["schemas"]["NeighborhoodFeatureCollection"];
export type NeighborhoodMetrics = components["schemas"]["NeighborhoodMetrics"];
export type NeighborhoodMetric = components["schemas"]["NeighborhoodMetric"];
export type ProblemDetails = components["schemas"]["ProblemDetails"];

const QUALITY_FLAGS = new Set(["ok", "estimated", "stale", "low_coverage", "inherited"]);

export function isMetricsResponse(value: unknown): value is MetricsResponse {
  return isRecord(value) && Array.isArray(value.metrics) && value.metrics.every(isMetricDefinition);
}

export function isMetricValues(value: unknown): value is MetricValues {
  return (
    isRecord(value) &&
    Object.values(value).every(
      (valuesByNeighborhood) =>
        isRecord(valuesByNeighborhood) && Object.values(valuesByNeighborhood).every(isMetricValue),
    )
  );
}

export function isNeighborhoodMetrics(value: unknown): value is NeighborhoodMetrics {
  return (
    isRecord(value) &&
    typeof value.neighborhoodId === "number" &&
    Array.isArray(value.metrics) &&
    value.metrics.every(isNeighborhoodMetric)
  );
}

export function isNeighborhoodCollection(value: unknown): value is NeighborhoodCollection {
  return (
    isRecord(value) &&
    value.type === "FeatureCollection" &&
    Array.isArray(value.features) &&
    value.features.every(
      (feature) =>
        isRecord(feature) &&
        feature.type === "Feature" &&
        isRecord(feature.geometry) &&
        typeof feature.geometry.type === "string" &&
        Array.isArray(feature.geometry.coordinates) &&
        isRecord(feature.properties) &&
        typeof feature.properties.id === "number" &&
        typeof feature.properties.name === "string" &&
        typeof feature.properties.districtId === "number" &&
        typeof feature.properties.districtName === "string",
    )
  );
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    isRecord(value) &&
    (value.title === undefined || typeof value.title === "string") &&
    (value.detail === undefined || typeof value.detail === "string") &&
    (value.code === undefined || typeof value.code === "string")
  );
}

function isMetricDefinition(value: unknown): value is MetricDefinition {
  return (
    isRecord(value) &&
    typeof value.key === "string" &&
    typeof value.category === "string" &&
    typeof value.unit === "string" &&
    (value.nativeLevel === "province" ||
      value.nativeLevel === "district" ||
      value.nativeLevel === "neighborhood") &&
    (value.polarity === "higher_is_better" ||
      value.polarity === "lower_is_better" ||
      value.polarity === "neutral") &&
    isRecord(value.normalization) &&
    typeof value.normalization.method === "string" &&
    typeof value.normalization.version === "string" &&
    typeof value.displayPrecision === "number" &&
    (value.observedAt === null || typeof value.observedAt === "string") &&
    Array.isArray(value.translations) &&
    value.translations.every(
      (translation) =>
        isRecord(translation) &&
        typeof translation.locale === "string" &&
        typeof translation.name === "string",
    ) &&
    isRecord(value.source) &&
    isMetricSource(value.source)
  );
}

function isMetricSource(value: Record<string, unknown>): value is MetricDefinition["source"] {
  return (
    typeof value.id === "string" &&
    typeof value.name === "string" &&
    typeof value.organization === "string" &&
    typeof value.url === "string" &&
    typeof value.license === "string" &&
    typeof value.attribution === "string" &&
    typeof value.methodology === "string" &&
    typeof value.retrievedAt === "string" &&
    typeof value.version === "string"
  );
}

function isMetricValue(value: unknown): value is MetricValue {
  return (
    isRecord(value) &&
    typeof value.raw === "number" &&
    typeof value.normalized === "number" &&
    typeof value.qualityFlag === "string" &&
    QUALITY_FLAGS.has(value.qualityFlag)
  );
}

function isNeighborhoodMetric(value: unknown): value is NeighborhoodMetric {
  return (
    isRecord(value) &&
    typeof value.key === "string" &&
    typeof value.raw === "number" &&
    typeof value.normalized === "number" &&
    typeof value.qualityFlag === "string" &&
    QUALITY_FLAGS.has(value.qualityFlag) &&
    typeof value.observedAt === "string" &&
    typeof value.referencePeriod === "string" &&
    isRecord(value.source) &&
    typeof value.source.id === "string" &&
    typeof value.source.name === "string" &&
    typeof value.source.organization === "string" &&
    typeof value.source.license === "string" &&
    typeof value.source.attribution === "string"
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}
