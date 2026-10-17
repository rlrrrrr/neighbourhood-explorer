import { useState } from "react";
import { fbt } from "fbtee";
import { Button } from "../../components/ui/button";
import { SAMPLE_NEIGHBORHOODS } from "../neighborhood-detail/sample-neighborhoods";
import { LensPanel } from "./lens-panel";

export function MapView() {
  const [showRanking, setShowRanking] = useState(true);
  const [showLens, setShowLens] = useState(false);
  const rankingTitle = fbt("Top 3 pour tes critères", "Title of the sample ranking on the map");

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
          className="grain absolute left-4 top-16 w-72 rounded-2xl bg-panel-bg p-4 shadow-elevated backdrop-blur"
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

      {showLens && <LensPanel onClose={() => setShowLens(false)} />}

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
