import { useLayoutEffect, useState } from "react";
import { isDarkTheme } from "../../app/theme";
import { useCity } from "../metrics/use-city";
import type { Criteria } from "../criteria/criteria";
import { readChoroplethColors, type ChoroplethColors } from "./choropleth-style";
import { DEFAULT_METRIC_KEY } from "../metrics/default-metric";
import { MapViewContent } from "./map-view-content";
import { useDocumentTheme } from "./use-document-theme";
import { useMapData, type MapDataState } from "./use-map-data";
import { useNeighborhoodMetrics } from "./use-neighborhood-metrics";

interface MapViewProps {
  criteria: Criteria;
  onRemoveCriterion: (metricKey: string) => void;
  selectedNeighborhoodId: number | null;
  onSelectNeighborhood: (neighborhoodId: number | null) => void;
  rankingMetricKey: string | null;
  onMetricChange: (metricKey: string) => void;
}

export function MapView({
  criteria,
  onRemoveCriterion,
  selectedNeighborhoodId,
  onSelectNeighborhood,
  rankingMetricKey,
  onMetricChange,
}: MapViewProps) {
  const themeName = useDocumentTheme();
  const { state: city, reload: reloadCity } = useCity();

  const { state, reload } = useMapData({
    cityId: city.status === "ready" ? city.city.plateCode : null,
    metricKey: rankingMetricKey ?? DEFAULT_METRIC_KEY,
    fallbackMetricKey: DEFAULT_METRIC_KEY,
  });

  const viewState: MapDataState =
    city.status === "error" ? { status: "error", code: city.code } : state;

  const { state: neighborhoodMetrics, reload: reloadNeighborhoodMetrics } = useNeighborhoodMetrics({
    neighborhoodId: selectedNeighborhoodId,
  });

  const [colors, setColors] = useState<ChoroplethColors | null>(null);

  useLayoutEffect(() => {
    // getComputedStyle only reflects the new theme after React commits it to the DOM.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setColors(readChoroplethColors(document.documentElement));
  }, [themeName]);

  return (
    <MapViewContent
      cityName={city.status === "ready" ? city.city.name : "—"}
      cityCenter={city.status === "ready" ? city.city.center : null}
      metricKey={rankingMetricKey ?? DEFAULT_METRIC_KEY}
      criteria={criteria}
      onRemoveCriterion={onRemoveCriterion}
      selectedNeighborhoodId={selectedNeighborhoodId}
      onSelectNeighborhood={onSelectNeighborhood}
      onMetricChange={onMetricChange}
      state={viewState}
      onReload={() => {
        reloadCity();
        reload();
      }}
      neighborhoodMetrics={neighborhoodMetrics}
      onReloadNeighborhoodMetrics={reloadNeighborhoodMetrics}
      colors={colors}
      documentTheme={isDarkTheme(themeName) ? "dark" : "light"}
    />
  );
}
