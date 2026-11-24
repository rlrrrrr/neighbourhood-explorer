import { useMemo, useRef, useState } from "react";
import { fbt, useLocaleContext } from "fbtee";
import { ChevronDown, LocateFixed } from "lucide-react";
import { Map, MapControls, type MapRef } from "../../components/ui/map";
import { Button } from "../../components/ui/button";
import { cn } from "../../components/ui/cn";
import type { Criteria } from "../criteria/criteria";
import type { City } from "../metrics/metric-api-schema";
import {
  collectionBounds,
  selectFeaturesById,
  type ChoroplethFeatureCollection,
} from "./choropleth";
import type { ChoroplethColors } from "./choropleth-style";
import { ChoroplethLayers } from "./choropleth-layers";
import { mapCameraAnimationDuration } from "./map-camera-duration";
import {
  formatMetricValue,
  localizedMetricName,
  localizedUnit,
  shortLicense,
} from "../metrics/metric-presentation";
import { buildLegendStops, LEGEND_SWATCH_CLASSES } from "./choropleth-legend";
import type { MapDataState } from "./use-map-data";
import type { NeighborhoodMetricsState } from "./use-neighborhood-metrics";
import { buildNeighborhoodMetricRows } from "./neighborhood-metric-rows";
import { NeighborhoodMetricsPanel } from "./neighborhood-metrics-panel";
import { LensPanel } from "./lens-panel";

const CITY_MAP_ZOOM = 10;

const basemapStyleUrl = import.meta.env.VITE_BASEMAP_STYLE_URL;
const basemapDarkStyleUrl = import.meta.env.VITE_BASEMAP_DARK_STYLE_URL;

const basemapStyles = basemapStyleUrl
  ? {
      light: basemapStyleUrl,
      dark: basemapDarkStyleUrl ?? basemapStyleUrl,
    }
  : undefined;

interface MapViewContentProps {
  cityName: string;
  cityCenter: City["center"] | null;
  metricKey: string;
  criteria: Criteria;
  onRemoveCriterion: (metricKey: string) => void;
  selectedNeighborhoodId: number | null;
  onSelectNeighborhood: (neighborhoodId: number | null) => void;
  onMetricChange: (metricKey: string) => void;
  state: MapDataState;
  onReload: () => void;
  neighborhoodMetrics: NeighborhoodMetricsState;
  onReloadNeighborhoodMetrics: () => void;
  colors: ChoroplethColors | null;
  documentTheme: "light" | "dark";
}

export function MapViewContent({
  cityName,
  cityCenter,
  metricKey,
  criteria,
  onRemoveCriterion,
  selectedNeighborhoodId,
  onSelectNeighborhood,
  onMetricChange,
  state,
  onReload,
  neighborhoodMetrics,
  onReloadNeighborhoodMetrics,
  colors,
  documentTheme,
}: MapViewContentProps) {
  const [showPanel, setShowPanel] = useState(true);
  const [showLens, setShowLens] = useState(false);
  const mapRef = useRef<MapRef | null>(null);
  const { locale } = useLocaleContext();

  const mapTitle = fbt("Carte des quartiers", "Title of the map region and its accessible name");
  const panelTitle = fbt("Quartiers", "Title of the neighborhood picker on the map");
  const loadingLabel = fbt("Chargement des quartiers…", "Shown while the map data is loading");

  const errorLabel = fbt(
    "La carte n'a pas pu charger les données.",
    "Shown when the neighborhood data cannot be loaded",
  );

  const data = state.status === "ready" ? state.data : null;

  const selectedFeature =
    data === null || selectedNeighborhoodId === null
      ? undefined
      : data.collection.features.find(
          (feature) => feature.properties.id === selectedNeighborhoodId,
        );

  const selectedCollection = useMemo(
    () => (data === null ? null : selectFeaturesById(data.collection, selectedNeighborhoodId)),
    [data, selectedNeighborhoodId],
  );

  const sortedFeatures = useMemo(
    () => (data === null ? [] : sortFeaturesByName(data.collection, locale)),
    [data, locale],
  );

  const neighborhoodRows = useMemo(
    () =>
      data !== null && neighborhoodMetrics.status === "ready"
        ? buildNeighborhoodMetricRows({
            definitions: data.metrics,
            metrics: neighborhoodMetrics.data.metrics,
            locale,
          })
        : [],
    [data, neighborhoodMetrics, locale],
  );

  const metricName = data === null ? "" : localizedMetricName(data.metric, locale);
  const legendStops = data === null ? [] : buildLegendStops(data.collection);
  const legendPrecision = data === null ? 0 : data.metric.displayPrecision;

  function recenterOnCity() {
    const clearsAnimatedSelection =
      colors !== null && selectedCollection !== null && selectedCollection.features.length > 0;

    onSelectNeighborhood(null);

    if (clearsAnimatedSelection) {
      return;
    }

    const map = mapRef.current;

    if (map === null || data === null) {
      return;
    }

    const bounds = collectionBounds(data.collection);

    if (bounds === null) {
      return;
    }

    map.fitBounds(bounds, { padding: 32, maxZoom: 11, duration: mapCameraAnimationDuration() });
  }

  return (
    <div
      role="region"
      aria-labelledby="map-title"
      className="relative flex-1 overflow-hidden bg-surface"
    >
      <h1 id="map-title" className="sr-only">
        {mapTitle}
      </h1>

      {cityCenter !== null ? (
        <div className="absolute inset-0">
          <Map
            ref={mapRef}
            center={[cityCenter.longitude, cityCenter.latitude]}
            zoom={CITY_MAP_ZOOM}
            theme={documentTheme}
            styles={basemapStyles}
            blank={basemapStyles === undefined}
            loading={state.status === "loading"}
            className="neighborhood-map h-full w-full"
          >
            <MapControls position="top-right" showZoom showCompass={false} />
            {data !== null && colors !== null ? (
              <ChoroplethLayers
                collection={data.collection}
                metricKey={metricKey}
                selectedCollection={selectedCollection}
                colors={colors}
                onSelectNeighborhood={onSelectNeighborhood}
              />
            ) : null}
          </Map>
        </div>
      ) : null}

      <div className="absolute start-4 top-4 z-20 flex gap-2">
        <Button
          size="sm"
          variant={showPanel ? "primary" : "outline"}
          aria-pressed={showPanel}
          onClick={() => setShowPanel((open) => !open)}
        >
          <fbt desc="Toggle showing the neighborhood picker">Quartiers</fbt>
        </Button>
        <Button
          size="sm"
          variant={showLens ? "primary" : "outline"}
          aria-pressed={showLens}
          onClick={() => setShowLens((open) => !open)}
        >
          <fbt desc="Toggle showing the lens panel">La lentille</fbt>
        </Button>
      </div>

      <aside
        data-open={showPanel ? "" : undefined}
        inert={!showPanel}
        className={cn(
          "anchored-panel anchored-panel-origin-start material-panel grain absolute start-4 top-16 z-20 max-h-[calc(100%-6rem)] w-72 overflow-y-auto rounded-2xl p-3 shadow-elevated",
          showLens && "max-sm:hidden",
        )}
        aria-label={panelTitle}
      >
        <h2 className="px-2 pt-1 text-sm font-bold">{panelTitle}</h2>

        {state.status === "loading" ? (
          <p className="px-2 py-3 text-sm text-text-secondary">{loadingLabel}</p>
        ) : null}

        {state.status === "error" ? (
          <div className="px-2 py-3">
            <p className="text-sm text-text-secondary">{errorLabel}</p>
            <Button size="sm" variant="outline" className="mt-2" onClick={onReload}>
              <fbt desc="Retry loading the map data">Réessayer</fbt>
            </Button>
          </div>
        ) : null}

        {data !== null ? (
          <>
            <label
              htmlFor="neighborhood-select"
              className="mt-2 block px-2 text-xs font-semibold text-text-secondary"
            >
              <fbt desc="Label of the neighborhood picker list">Aller à un quartier</fbt>
            </label>
            <div className="group relative mt-1">
              <select
                id="neighborhood-select"
                value={selectedNeighborhoodId ?? ""}
                onChange={(event) => {
                  if (event.target.value !== "") {
                    onSelectNeighborhood(Number(event.target.value));
                  }
                }}
                className="min-h-11 w-full appearance-none rounded-xl border border-border bg-surface px-3 pe-10 text-sm text-text transition-[border-color] duration-[160ms] ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action motion-reduce:transition-none [&:open]:border-action"
              >
                <option value="">
                  <fbt desc="Empty option in the neighborhood picker">Choisir un quartier</fbt>
                </option>
                {sortedFeatures.map((feature) => (
                  <option key={feature.properties.id} value={feature.properties.id}>
                    {feature.properties.name} · {feature.properties.districtName}
                  </option>
                ))}
              </select>
              <ChevronDown
                aria-hidden="true"
                className="pointer-events-none absolute end-3 top-1/2 size-4 -translate-y-1/2 text-text-secondary transition-transform duration-[160ms] ease-out group-has-[select:open]:rotate-180 motion-reduce:transition-none"
              />
            </div>
            <Button size="sm" variant="outline" className="mt-2 w-full" onClick={recenterOnCity}>
              <LocateFixed aria-hidden="true" className="size-4" />
              <fbt desc="Button that recenters the map on the loaded neighborhoods">
                Recentrer sur la ville
              </fbt>
            </Button>

            {selectedFeature !== undefined ? (
              <div className="mt-3">
                <NeighborhoodMetricsPanel
                  neighborhoodName={selectedFeature.properties.name}
                  districtName={selectedFeature.properties.districtName}
                  rows={neighborhoodRows}
                  state={neighborhoodMetrics}
                  locale={locale}
                  onReload={onReloadNeighborhoodMetrics}
                />
              </div>
            ) : (
              <p className="px-2 pt-3 text-sm text-text-secondary">
                <fbt desc="Hint shown when no neighborhood is selected on the map">
                  Clique un quartier sur la carte ou choisis-le dans la liste.
                </fbt>
              </p>
            )}
            <details className="mt-3 rounded-xl bg-surface p-3 text-xs text-text-secondary">
              <summary className="cursor-pointer font-semibold text-text">
                <fbt desc="Summary of the data source and method panel on the map">
                  Source et méthode
                </fbt>
              </summary>
              <p className="mt-2">{data.metric.source.name}</p>
              <p className="mt-1">{data.metric.source.attribution}</p>
              <p className="mt-1">{data.metric.source.license}</p>
              <p className="mt-1">{data.metric.source.methodology}</p>
              <p className="mt-1">
                <fbt desc="Observation date label in the source panel">Observation :</fbt>{" "}
                {data.metric.observedAt ?? "—"}
              </p>
              <p className="mt-1">
                <fbt desc="Retrieval date label in the source panel">Récupération :</fbt>{" "}
                {data.metric.source.retrievedAt}
              </p>
              <a
                href={data.metric.source.url}
                target="_blank"
                rel="noreferrer"
                className="mt-2 block break-all underline"
              >
                {data.metric.source.url}
              </a>
            </details>
          </>
        ) : null}
      </aside>

      <LensPanel
        cityName={cityName}
        observedAt={data?.metric.observedAt ?? null}
        criteria={criteria}
        onRemoveCriterion={onRemoveCriterion}
        metrics={data?.metrics ?? []}
        selectedMetricKey={data?.metric.key ?? ""}
        onSelectMetric={onMetricChange}
        open={showLens}
        onClose={() => setShowLens(false)}
      />

      <div className="pointer-events-none absolute bottom-0 left-0 z-10 flex max-w-[min(100%,28rem)] flex-col gap-2 p-3 sm:p-4">
        {data !== null ? (
          <p
            key={data.metric.key}
            className="metric-caption-enter material-panel pointer-events-auto rounded-2xl px-3 py-1.5 text-xs text-text-secondary"
          >
            {metricName} · {data.metric.source.organization} ·{" "}
            {shortLicense(data.metric.source.license)} · {data.metric.observedAt ?? "—"}
          </p>
        ) : null}
        <div className="material-panel pointer-events-auto flex flex-col gap-2 rounded-2xl border border-border px-3 py-2 text-xs">
          <div>
            <div className="grid grid-cols-5 gap-1" aria-hidden="true">
              {LEGEND_SWATCH_CLASSES.map((swatchClass) => (
                <span
                  key={swatchClass}
                  className={cn("h-2 w-full rounded-sm ring-1 ring-border", swatchClass)}
                />
              ))}
            </div>
            <div className="mt-1 grid grid-cols-5 gap-1">
              {LEGEND_SWATCH_CLASSES.map((swatchClass, index) => {
                const stop = legendStops[index];

                return (
                  <span
                    key={swatchClass}
                    className="text-center text-[10px] leading-tight text-text-secondary tabular-nums"
                  >
                    {stop === undefined || stop === null
                      ? "—"
                      : formatMetricValue({
                          value: stop,
                          locale,
                          maximumFractionDigits: legendPrecision,
                        })}
                  </span>
                );
              })}
            </div>
            {data !== null ? (
              <p className="mt-0.5 text-[10px] text-text-secondary">
                {localizedUnit(data.metric.unit)}
              </p>
            ) : null}
          </div>
          <span className="flex items-center gap-1.5">
            <span className="no-data-pattern h-2 w-4 rounded-sm ring-1 ring-border" />
            <fbt desc="Legend label for neighborhoods without data">Pas de donnée</fbt>
          </span>
        </div>
      </div>
    </div>
  );
}

function sortFeaturesByName(
  collection: ChoroplethFeatureCollection,
  locale: string,
): ChoroplethFeatureCollection["features"] {
  return [...collection.features].sort((left, right) =>
    left.properties.name.localeCompare(right.properties.name, locale),
  );
}
