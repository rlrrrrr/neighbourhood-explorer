import {
  isMetricValues,
  isMetricsResponse,
  isNeighborhoodCollection,
  isProblemDetails,
  type MetricDefinition,
  type MetricValue,
  type MetricValues,
  type NeighborhoodCollection,
  type ProblemDetails,
} from "../metrics/metric-api-schema";

const API_BASE_URL = import.meta.env.VITE_API_URL ?? "";

export type DataTableValue = {
  raw: number;
  qualityFlag: MetricValue["qualityFlag"];
};

export type DataTableRow = {
  neighborhoodId: number;
  name: string;
  districtName: string;
  values: Readonly<Record<string, DataTableValue>>;
};

export type DataTableData = {
  metrics: readonly MetricDefinition[];
  rows: readonly DataTableRow[];
  unknownMetricKeys: readonly string[];
};

export class DataTableRequestError extends Error {
  readonly code: string;

  constructor({ code, message }: { code: string; message: string }) {
    super(message);
    this.name = "DataTableRequestError";
    this.code = code;
  }
}

export async function fetchDataTable({
  cityId,
  metricKeys,
  signal,
}: {
  cityId: number;
  metricKeys: readonly string[];
  signal: AbortSignal;
}): Promise<DataTableData> {
  if (metricKeys.length === 0) {
    return { metrics: [], rows: [], unknownMetricKeys: [] };
  }

  const metricsPayload = await getJson("/api/v1/metrics", signal);

  if (!isMetricsResponse(metricsPayload)) {
    throw new DataTableRequestError({
      code: "invalid_response",
      message: "La réponse de /api/v1/metrics est invalide.",
    });
  }

  const metrics = metricKeys.flatMap((metricKey) => {
    const metric = metricsPayload.metrics.find((candidate) => candidate.key === metricKey);
    return metric === undefined ? [] : [metric];
  });
  const knownMetricKeys = metrics.map((metric) => metric.key);
  const unknownMetricKeys = metricKeys.filter((metricKey) => !knownMetricKeys.includes(metricKey));

  if (knownMetricKeys.length === 0) {
    return { metrics, rows: [], unknownMetricKeys };
  }

  const [neighborhoodsPayload, valuesPayload] = await Promise.all([
    getJson(`/api/v1/cities/${cityId}/neighborhoods`, signal),
    fetchMetricValues({ cityId, knownMetricKeys, signal }),
  ]);

  if (!isNeighborhoodCollection(neighborhoodsPayload)) {
    throw new DataTableRequestError({
      code: "invalid_response",
      message: "La réponse GeoJSON des quartiers est invalide.",
    });
  }

  return {
    metrics,
    unknownMetricKeys,
    rows: buildRows({ neighborhoodsPayload, valuesPayload, knownMetricKeys }),
  };
}

async function fetchMetricValues({
  cityId,
  knownMetricKeys,
  signal,
}: {
  cityId: number;
  knownMetricKeys: readonly string[];
  signal: AbortSignal;
}): Promise<MetricValues> {
  if (knownMetricKeys.length === 0) {
    return {};
  }

  const encodedKeys = knownMetricKeys.map((metricKey) => encodeURIComponent(metricKey)).join(",");
  const payload = await getJson(
    `/api/v1/cities/${cityId}/metric-values?metrics=${encodedKeys}`,
    signal,
  );

  if (!isMetricValues(payload)) {
    throw new DataTableRequestError({
      code: "invalid_response",
      message: "La réponse des valeurs de métriques est invalide.",
    });
  }

  return payload;
}

function buildRows({
  neighborhoodsPayload,
  valuesPayload,
  knownMetricKeys,
}: {
  neighborhoodsPayload: NeighborhoodCollection;
  valuesPayload: MetricValues;
  knownMetricKeys: readonly string[];
}): DataTableRow[] {
  return neighborhoodsPayload.features.map((feature) => ({
    neighborhoodId: feature.properties.id,
    name: feature.properties.name,
    districtName: feature.properties.districtName,
    values: readValues({
      valuesPayload,
      knownMetricKeys,
      neighborhoodId: feature.properties.id,
    }),
  }));
}

function readValues({
  valuesPayload,
  knownMetricKeys,
  neighborhoodId,
}: {
  valuesPayload: MetricValues;
  knownMetricKeys: readonly string[];
  neighborhoodId: number;
}): Record<string, DataTableValue> {
  const values: Record<string, DataTableValue> = {};
  for (const metricKey of knownMetricKeys) {
    const value = valuesPayload[metricKey]?.[String(neighborhoodId)];
    if (value !== undefined) {
      values[metricKey] = { raw: value.raw, qualityFlag: value.qualityFlag };
    }
  }
  return values;
}

async function getJson(path: string, signal: AbortSignal): Promise<unknown> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    signal,
    headers: { Accept: "application/json, application/geo+json" },
  });

  if (!response.ok) {
    const problem = await readProblemDetails(response);
    throw new DataTableRequestError({
      code: problem?.code ?? `http_${response.status}`,
      message: problem?.detail ?? `L'API a répondu ${response.status}.`,
    });
  }

  return response.json();
}

async function readProblemDetails(response: Response): Promise<ProblemDetails | null> {
  try {
    const payload: unknown = await response.json();
    return isProblemDetails(payload) ? payload : null;
  } catch {
    return null;
  }
}
