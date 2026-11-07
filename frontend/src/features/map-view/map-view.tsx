import { useEffect, useState } from "react";
import { isDarkTheme } from "../../app/theme";
import type { Criteria } from "../criteria/criteria";
import { readChoroplethColors, type ChoroplethColors } from "./choropleth-style";
import { POPULATION_DENSITY_METRIC_KEY } from "./choropleth";
import { MapViewContent } from "./map-view-content";
import { useDocumentTheme } from "./use-document-theme";
import { useMapData } from "./use-map-data";

const ISTANBUL_CITY_ID = 34;

interface MapViewProps {
  criteria: Criteria;
  onRemoveCriterion: (metricKey: string) => void;
  selectedNeighborhoodId: number | null;
  onSelectNeighborhood: (neighborhoodId: number | null) => void;
}

export function MapView({
  criteria,
  onRemoveCriterion,
  selectedNeighborhoodId,
  onSelectNeighborhood,
}: MapViewProps) {
  const themeName = useDocumentTheme();
  const { state, reload } = useMapData({
    cityId: ISTANBUL_CITY_ID,
    metricKey: POPULATION_DENSITY_METRIC_KEY,
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
      state={state}
      onReload={reload}
      colors={colors}
      documentTheme={isDarkTheme(themeName) ? "dark" : "light"}
    />
  );
}
