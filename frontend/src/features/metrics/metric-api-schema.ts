import * as z from "zod";
import type { components } from "../../generated/api";

export type MetricDefinition = components["schemas"]["MetricDefinition"];

export type MetricValue = components["schemas"]["MetricValue"];

export type MetricValues = components["schemas"]["MetricValues"];

export type MetricsResponse = components["schemas"]["MetricsResponse"];

export type CitiesResponse = components["schemas"]["CitiesResponse"];

export type City = components["schemas"]["City"];

export type NeighborhoodCollection = components["schemas"]["NeighborhoodFeatureCollection"];

export type NeighborhoodMetrics = components["schemas"]["NeighborhoodMetrics"];

export type NeighborhoodMetric = components["schemas"]["NeighborhoodMetric"];

export type ProblemDetails = components["schemas"]["ProblemDetails"];

const qualityFlagSchema = z.enum(["ok", "estimated", "stale", "low_coverage", "inherited"]);

const metricSourceSchema = z.object({
  id: z.string(),
  name: z.string(),
  organization: z.string(),
  url: z.string(),
  license: z.string(),
  attribution: z.string(),
  methodology: z.string(),
  retrievedAt: z.string(),
  version: z.string(),
});

const metricNormalizationSchema = z.object({
  method: z.string(),
  version: z.string(),
});

const metricTranslationSchema = z.object({
  locale: z.enum(["fr", "en", "tr"]),
  name: z.string(),
  description: z.string(),
  subject: z.string(),
});

const metricDefinitionSchema = z.object({
  key: z.string(),
  category: z.string(),
  unit: z.string(),
  nativeLevel: z.enum(["province", "district", "neighborhood"]),
  polarity: z.enum(["higher_is_better", "lower_is_better", "neutral"]),
  normalization: metricNormalizationSchema,
  displayPrecision: z.number(),
  translations: z.array(metricTranslationSchema),
  source: metricSourceSchema,
  observedAt: z.nullable(z.string()),
});

const metricValueSchema = z.object({
  raw: z.number(),
  normalized: z.number(),
  qualityFlag: qualityFlagSchema,
});

const citySchema = z.object({
  plateCode: z.number(),
  name: z.string(),
  officialCode: z.string(),
  center: z.object({ longitude: z.number(), latitude: z.number() }),
});

export const citiesResponseSchema = z.object({
  cities: z.array(citySchema),
}) satisfies z.ZodType<CitiesResponse>;

export const metricsResponseSchema = z.object({
  metrics: z.array(metricDefinitionSchema),
}) satisfies z.ZodType<MetricsResponse>;

export const metricValuesSchema = z.record(
  z.string(),
  z.record(z.string(), metricValueSchema),
) satisfies z.ZodType<MetricValues>;

const geoJsonGeometrySchema = z.object({
  type: z.enum(["Point", "Polygon", "MultiPolygon"]),
  coordinates: z.array(z.unknown()),
});

const neighborhoodFeatureSchema = z.object({
  type: z.literal("Feature"),
  geometry: geoJsonGeometrySchema,
  properties: z.object({
    id: z.number(),
    name: z.string(),
    districtId: z.number(),
    districtName: z.string(),
  }),
});

export const neighborhoodCollectionSchema = z.object({
  type: z.literal("FeatureCollection"),
  features: z.array(neighborhoodFeatureSchema),
}) satisfies z.ZodType<NeighborhoodCollection>;

const neighborhoodMetricSourceSchema = z.object({
  id: z.string(),
  name: z.string(),
  organization: z.string(),
  license: z.string(),
  attribution: z.string(),
});

const neighborhoodMetricSchema = z.object({
  key: z.string(),
  raw: z.number(),
  normalized: z.number(),
  qualityFlag: qualityFlagSchema,
  observedAt: z.string(),
  referencePeriod: z.string(),
  source: neighborhoodMetricSourceSchema,
});

export const neighborhoodMetricsSchema = z.object({
  neighborhoodId: z.number(),
  metrics: z.array(neighborhoodMetricSchema),
}) satisfies z.ZodType<NeighborhoodMetrics>;

export const problemDetailsSchema = z.object({
  type: z.string().optional(),
  title: z.string().optional(),
  status: z.number().optional(),
  detail: z.string().optional(),
  instance: z.string().optional(),
  code: z.string().optional(),
}) satisfies z.ZodType<ProblemDetails>;
