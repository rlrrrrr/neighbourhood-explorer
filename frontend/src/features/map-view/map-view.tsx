import { useEffect, useState } from "react";
import { isDarkTheme } from "../../app/theme";
import type { Criteria } from "../criteria/criteria";
import { readChoroplethColors, type ChoroplethColors } from "./choropleth-style";
import { POPULATION_DENSITY_METRIC_KEY } from "./choropleth";
import { MapViewContent } from "./map-view-content";
import { useDocumentTheme } from "./use-document-theme";
import { useMapData } from "./use-map-data";
import { useNeighborhoodMetrics } from "./use-neighborhood-metrics";

const ISTANBUL_CITY_ID = 34;

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
  const { state, reload } = useMapData({
    cityId: ISTANBUL_CITY_ID,
    metricKey: rankingMetricKey ?? POPULATION_DENSITY_METRIC_KEY,
    fallbackMetricKey: POPULATION_DENSITY_METRIC_KEY,
  });
  const { state: neighborhoodMetrics, reload: reloadNeighborhoodMetrics } = useNeighborhoodMetrics({
    neighborhoodId: selectedNeighborhoodId,
  });
  const [colors, setColors] = useState<ChoroplethColors | null>(null);

  useEffect(() => {
    setColors(readChoroplethColors(document.documentElement));
  }, [themeName]);

  return (
    <MapViewContent
      criteria={criteria}
      onRemoveCriterion={onRemoveCriterion}
      selectedNeighborhoodId={selectedNeighborhoodId}
      onSelectNeighborhood={onSelectNeighborhood}
      onMetricChange={onMetricChange}
      state={state}
      onReload={reload}
      neighborhoodMetrics={neighborhoodMetrics}
      onReloadNeighborhoodMetrics={reloadNeighborhoodMetrics}
      colors={colors}
      documentTheme={isDarkTheme(themeName) ? "dark" : "light"}
    />
  );
}
