import { useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiRequestError } from "../metrics/api-json";
import { fetchMapData, type MapData } from "./map-data-client";

export type MapDataState =
  | { status: "loading" }
  | { status: "error"; code: string }
  | { status: "ready"; data: MapData };

export function useMapData({
  cityId,
  metricKey,
  fallbackMetricKey,
}: {
  cityId: number;
  metricKey: string;
  fallbackMetricKey: string;
}): { state: MapDataState; reload: () => void } {
  const query = useQuery({
    queryKey: ["map-data", cityId, metricKey],
    queryFn: ({ signal }) => fetchMapData({ cityId, metricKey, fallbackMetricKey, signal }),
  });

  const { refetch } = query;

  const reload = useCallback(() => {
    void refetch();
  }, [refetch]);

  if (query.status === "pending") {
    return { state: { status: "loading" }, reload };
  }

  if (query.status === "error") {
    return {
      state: {
        status: "error",
        code: query.error instanceof ApiRequestError ? query.error.code : "network_error",
      },
      reload,
    };
  }

  return { state: { status: "ready", data: query.data }, reload };
}
