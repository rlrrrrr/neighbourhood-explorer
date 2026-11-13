import { useCallback, useEffect, useState } from "react";
import { fetchDataTable, DataTableRequestError, type DataTableData } from "./data-table-client";

export type DataTableState =
  | { status: "idle" }
  | { status: "loading" }
  | { status: "error"; code: string }
  | { status: "ready"; data: DataTableData };

type SettledState = Exclude<DataTableState, { status: "idle" } | { status: "loading" }>;

type SettledResult = {
  requestKey: string;
  state: SettledState;
};

export function useDataTable({
  cityId,
  metricKeys,
}: {
  cityId: number;
  metricKeys: readonly string[];
}): { state: DataTableState; reload: () => void } {
  const [reloadToken, setReloadToken] = useState(0);
  const [result, setResult] = useState<SettledResult | null>(null);
  const metricKeyList = metricKeys.join(",");
  const requestKey = `${cityId}:${reloadToken}:${metricKeyList}`;

  useEffect(() => {
    if (metricKeyList === "") {
      return;
    }

    const controller = new AbortController();

    fetchDataTable({ cityId, metricKeys: metricKeyList.split(","), signal: controller.signal })
      .then((data) => setResult({ requestKey, state: { status: "ready", data } }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return;
        }
        setResult({
          requestKey,
          state: {
            status: "error",
            code: error instanceof DataTableRequestError ? error.code : "network_error",
          },
        });
      });

    return () => controller.abort();
  }, [cityId, metricKeyList, requestKey]);

  const state: DataTableState =
    metricKeyList === ""
      ? { status: "idle" }
      : result?.requestKey === requestKey
        ? result.state
        : { status: "loading" };

  return { state, reload: useCallback(() => setReloadToken((token) => token + 1), []) };
}
