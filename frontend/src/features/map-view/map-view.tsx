import { useState } from "react";
import { fbt } from "fbtee";
import { Button } from "../../components/ui/button";
import { SAMPLE_NEIGHBORHOODS } from "../neighborhood-detail/sample-neighborhoods";

const SAMPLE_CITY = "Istanbul";

const LENS_BASEMAP_STYLES = ["Positron", "Liberty", "Dark"] as const;

const LENS_CATEGORIES = [
  { key: "mobility", label: () => fbt("Mobilité", "Lens filter category: mobility") },
  { key: "local-life", label: () => fbt("Vie locale", "Lens filter category: local life") },
  { key: "nature", label: () => fbt("Nature", "Lens filter category: nature") },
  { key: "services", label: () => fbt("Services", "Lens filter category: services") },
];

const LENS_LAYERS = [
  { key: "metro", label: () => fbt("Métro", "Lens layer: metro") },
  { key: "parks", label: () => fbt("Parcs", "Lens layer: parks") },
  { key: "isochrone", label: () => fbt("Isochrone", "Lens layer: isochrone") },
];

export function MapView() {
  const [showRanking, setShowRanking] = useState(true);
  const [showLens, setShowLens] = useState(false);
  const rankingTitle = fbt("Top 3 pour tes critères", "Title of the sample ranking on the map");
  const lensTitle = fbt("La lentille", "Title of the lens panel on the map");

  return (
    <div className="relative flex-1 bg-surface">
      <div className="absolute inset-0 flex items-center justify-center">
        <p className="text-sm text-text-secondary">
          <fbt desc="Placeholder shown while the map is not loaded">
            La carte s'affichera ici — fond Positron (OpenFreeMap)
          </fbt>
        </p>
      </div>

      <div className="absolute left-4 top-4 flex gap-2">
        <Button
          size="sm"
          variant={showRanking ? "primary" : "outline"}
          aria-pressed={showRanking}
          onClick={() => setShowRanking((open) => !open)}
        >
          <fbt desc="Toggle showing the sample ranking">Classement</fbt>
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

      {showRanking && (
        <aside
          className="grain absolute left-4 top-16 w-72 rounded-2xl border border-border bg-panel-bg p-4 shadow-elevated backdrop-blur"
          aria-label={rankingTitle}
        >
          <h2 className="text-sm font-bold">{rankingTitle}</h2>
          <ol className="mt-3 flex flex-col gap-2">
            {SAMPLE_NEIGHBORHOODS.map((result, index) => (
              <li key={result.name} className="flex items-center gap-3">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-highlight text-xs font-bold text-highlight-text">
                  {index + 1}
                </span>
                <span className="text-sm font-semibold">{result.name}</span>
                <span className="text-xs text-text-secondary">{result.district}</span>
              </li>
            ))}
          </ol>
        </aside>
      )}

      {showLens && (
        <aside
          className="absolute right-4 top-16 max-h-[calc(100%-8rem)] w-72 overflow-y-auto rounded-2xl border border-border bg-panel-bg p-4 shadow-elevated backdrop-blur"
          aria-label={lensTitle}
        >
          <h2 className="text-sm font-bold">{lensTitle}</h2>

          <div className="mt-4 flex items-baseline justify-between">
            <span className="text-xs font-semibold text-text-secondary">
              <fbt desc="Label of the city field in the lens panel">Ville</fbt>
            </span>
            <span className="text-sm font-semibold">{SAMPLE_CITY}</span>
          </div>
          <p className="mt-1 text-xs text-text-secondary">
            <fbt desc="Note explaining that no sample data has a time dimension">
              Période masquée : aucune donnée en cours n'a de dimension temporelle
            </fbt>
          </p>

          <fieldset className="mt-4">
            <legend className="text-xs font-semibold text-text-secondary">
              <fbt desc="Legend of the category filter group in the lens panel">Catégories</fbt>
            </legend>
            <div className="mt-2 flex flex-wrap gap-2">
              {LENS_CATEGORIES.map((category) => (
                <label
                  key={category.key}
                  className="flex items-center gap-1.5 rounded-full border border-border px-3 py-1 text-xs"
                >
                  <input type="checkbox" />
                  {category.label()}
                </label>
              ))}
            </div>
          </fieldset>

          <div className="mt-4">
            <span className="text-xs font-semibold text-text-secondary">
              <fbt desc="Label of the saved selection field in the lens panel">Sélection</fbt>
            </span>
            <p className="mt-1 text-xs text-text-secondary">
              <fbt desc="Empty state of the saved selection in the lens panel">
                Aucun quartier enregistré pour l'instant
              </fbt>
            </p>
          </div>

          <fieldset className="mt-4">
            <legend className="text-xs font-semibold text-text-secondary">
              <fbt desc="Legend of the basemap style group in the lens panel">Fond de carte</fbt>
            </legend>
            <div className="mt-2 flex flex-wrap gap-2">
              {LENS_BASEMAP_STYLES.map((style) => (
                <label
                  key={style}
                  className="flex items-center gap-1.5 rounded-full border border-border px-3 py-1 text-xs"
                >
                  <input type="radio" name="basemap" defaultChecked={style === "Positron"} />
                  {style}
                </label>
              ))}
            </div>
          </fieldset>

          <fieldset className="mt-4">
            <legend className="text-xs font-semibold text-text-secondary">
              <fbt desc="Legend of the layer group in the lens panel">Couches</fbt>
            </legend>
            <div className="mt-2 flex flex-wrap gap-2">
              {LENS_LAYERS.map((layer) => (
                <label
                  key={layer.key}
                  className="flex items-center gap-1.5 rounded-full border border-border px-3 py-1 text-xs"
                >
                  <input type="checkbox" />
                  {layer.label()}
                </label>
              ))}
            </div>
          </fieldset>
        </aside>
      )}

      <div className="absolute bottom-4 left-4 flex items-center gap-2 rounded-full border border-border bg-panel-bg px-3 py-2 text-xs backdrop-blur">
        <span>
          <fbt desc="Low end of the score ramp legend">Faible</fbt>
        </span>
        <span className="h-3 w-5 rounded-sm bg-ramp-1" />
        <span className="h-3 w-5 rounded-sm bg-ramp-2" />
        <span className="h-3 w-5 rounded-sm bg-ramp-3" />
        <span className="h-3 w-5 rounded-sm bg-ramp-4" />
        <span className="h-3 w-5 rounded-sm bg-ramp-5" />
        <span>
          <fbt desc="High end of the score ramp legend">Élevé</fbt>
        </span>
        <span className="ml-2 h-3 w-5 rounded-sm bg-no-data" />
        <span>
          <fbt desc="Legend label for neighborhoods without data">Pas de donnée</fbt>
        </span>
      </div>

      <p className="absolute bottom-4 right-4 text-xs text-text-secondary">
        <fbt desc="Basemap and data attribution shown on the map">
          Fond de carte : OpenFreeMap · Données © OpenStreetMap
        </fbt>
      </p>
    </div>
  );
}
