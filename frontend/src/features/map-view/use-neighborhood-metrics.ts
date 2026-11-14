import { useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiRequestError } from "../metrics/api-json";
import { fetchNeighborhoodMetrics } from "./neighborhood-metrics-client";
import type { NeighborhoodMetrics } from "../metrics/metric-api-schema";

export type NeighborhoodMetricsState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "error"; code: string }
  | { status: "ready"; data: NeighborhoodMetrics };

export function useNeighborhoodMetrics({ neighborhoodId }: { neighborhoodId: number | null }): {
  state: NeighborhoodMetricsState;
  reload: () => void;
} {
  const query = useQuery({
    queryKey: ["neighborhood-metrics", neighborhoodId],
    queryFn: ({ signal }) => {
      if (neighborhoodId === null) {
        return Promise.reject(new Error("no neighborhood selected"));
      }
      return fetchNeighborhoodMetrics({ neighborhoodId, signal });
    },
    enabled: neighborhoodId !== null,
  });

  const { refetch } = query;
  const reload = useCallback(() => {
    void refetch();
  }, [refetch]);

  if (neighborhoodId === null) {
    return { state: { status: "idle" }, reload };
  }
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
