import { useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiRequestError } from "./api-json";
import { fetchCities, selectDefaultCity } from "./city-client";
import type { City } from "./metric-api-schema";

export type CityState =
  | { status: "loading" }
  | { status: "error"; code: string }
  | { status: "ready"; city: City };

export function useCity(): { state: CityState; reload: () => void } {
  const query = useQuery({
    queryKey: ["cities"],
    queryFn: ({ signal }) => fetchCities({ signal }),
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

  const city = selectDefaultCity(query.data);

  return city === null
    ? { state: { status: "error", code: "no_city" }, reload }
    : { state: { status: "ready", city }, reload };
}
