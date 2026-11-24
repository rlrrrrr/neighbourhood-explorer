import { useCallback, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiRequestError } from "../metrics/api-json";
import { fetchCityMetricValues } from "../metrics/city-metric-values";
import { createMapData, fetchMapStatic, type MapData } from "./map-data-client";

export type MapDataState =
  | { status: "loading" }
  | { status: "error"; code: string }
  | { status: "ready"; data: MapData };

export type MapDataResult = {
  state: MapDataState;
  reload: () => void;
};

export function useMapData({
  cityId,
  metricKey,
  fallbackMetricKey,
}: {
  cityId: number | null;
  metricKey: string;
  fallbackMetricKey: string;
}): MapDataResult {
  const staticQuery = useQuery({
    queryKey: ["map-static", cityId],
    queryFn: ({ signal }) => {
      if (cityId === null) {
        throw new Error("ville indisponible");
      }

      return fetchMapStatic({ cityId, signal });
    },
    enabled: cityId !== null,
    staleTime: Infinity,
  });

  const metricKeys = staticQuery.data?.metrics.map((metric) => metric.key) ?? [];

  const valuesQuery = useQuery({
    queryKey: ["map-values", cityId],
    queryFn: ({ signal }) => {
      if (cityId === null || metricKeys.length === 0) {
        throw new Error("données de ville indisponibles");
      }

      return fetchCityMetricValues({ cityId, metricKeys, signal });
    },
    enabled: cityId !== null && metricKeys.length > 0,
    staleTime: Infinity,
  });

  const data = useMemo(() => {
    if (staticQuery.data === undefined || valuesQuery.data === undefined) {
      return null;
    }

    return createMapData({
      metrics: staticQuery.data.metrics,
      neighborhoods: staticQuery.data.neighborhoods,
      values: valuesQuery.data,
      metricKey,
      fallbackMetricKey,
    });
  }, [staticQuery.data, valuesQuery.data, metricKey, fallbackMetricKey]);

  const { refetch: reloadStatic } = staticQuery;
  const { refetch: reloadValues } = valuesQuery;

  const reload = useCallback(() => {
    void reloadStatic();
    void reloadValues();
  }, [reloadStatic, reloadValues]);

  if (staticQuery.status === "pending") {
    return { state: { status: "loading" }, reload };
  }

  if (staticQuery.status === "error") {
    return {
      state: {
        status: "error",
        code:
          staticQuery.error instanceof ApiRequestError ? staticQuery.error.code : "network_error",
      },
      reload,
    };
  }

  if (metricKeys.length === 0) {
    return { state: { status: "error", code: "metric_not_found" }, reload };
  }

  if (valuesQuery.status === "pending") {
    return { state: { status: "loading" }, reload };
  }

  if (valuesQuery.status === "error") {
    return {
      state: {
        status: "error",
        code:
          valuesQuery.error instanceof ApiRequestError ? valuesQuery.error.code : "network_error",
      },
      reload,
    };
  }

  if (data === null) {
    return { state: { status: "error", code: "metric_not_found" }, reload };
  }

  return { state: { status: "ready", data }, reload };
}
