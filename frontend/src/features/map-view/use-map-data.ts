import { useCallback, useEffect, useState } from "react";
import { fetchMapData, MapDataRequestError, type MapData } from "./map-data-client";

export type MapDataState =
  | { status: "loading" }
  | { status: "error"; code: string }
  | { status: "ready"; data: MapData };

export function useMapData({ cityId, metricKey }: { cityId: number; metricKey: string }): {
  state: MapDataState;
  reload: () => void;
} {
  const [state, setState] = useState<MapDataState>({ status: "loading" });
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    const controller = new AbortController();

    fetchMapData({ cityId, metricKey, signal: controller.signal })
      .then((data) => setState({ status: "ready", data }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return;
        }
        setState({
          status: "error",
          code: error instanceof MapDataRequestError ? error.code : "network_error",
        });
      });

    return () => controller.abort();
  }, [cityId, metricKey, reloadToken]);

  const reload = useCallback(() => {
    setState({ status: "loading" });
    setReloadToken((token) => token + 1);
  }, []);

  return { state, reload };
}
