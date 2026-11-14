import { useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiRequestError } from "../metrics/api-json";
import { fetchDataTable, type DataTableData } from "./data-table-client";

export type DataTableState =
  | { status: "loading" }
  | { status: "error"; code: string }
  | { status: "ready"; data: DataTableData };

export function useDataTable({ cityId }: { cityId: number }): {
  state: DataTableState;
  reload: () => void;
} {
  const query = useQuery({
    queryKey: ["data-table", cityId],
    queryFn: ({ signal }) => fetchDataTable({ cityId, signal }),
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
