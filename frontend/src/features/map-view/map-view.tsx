import { Activity, useState } from "react";
import { fbt } from "fbtee";
import { tv } from "tailwind-variants";
import { Button } from "../../components/ui/button";
import { cn } from "../../components/ui/cn";
import { SAMPLE_NEIGHBORHOODS } from "../neighborhood-detail/sample-neighborhoods";
import { LensPanel } from "./lens-panel";
import type { Criteria } from "../criteria/criteria";

const rankingRow = tv({
  base: "flex min-h-11 w-full cursor-pointer items-center gap-3 rounded-xl px-2 py-1.5 text-left transition-colors duration-150 ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action sm:min-h-0",
  variants: {
    selected: {
      true: "bg-highlight text-highlight-text",
      false: "hover:bg-surface",
    },
  },
  defaultVariants: {
    selected: false,
  },
});

interface MapViewProps {
  criteria: Criteria;
  onRemoveCriterion: (metricKey: string) => void;
  selectedNeighborhood: string | null;
  onSelectNeighborhood: (neighborhood: string) => void;
}

export function MapView({
  criteria,
  onRemoveCriterion,
  selectedNeighborhood,
  onSelectNeighborhood,
}: MapViewProps) {
  const [showRanking, setShowRanking] = useState(true);
  const [showLens, setShowLens] = useState(false);
  const mapTitle = fbt("Carte des quartiers", "Title of the map region and its accessible name");
  const rankingTitle = fbt("Top 3 pour tes critères", "Title of the sample ranking on the map");

  return (
    <div
      role="region"
      aria-labelledby="map-title"
      className="relative flex-1 overflow-hidden bg-surface"
    >
      <h1 id="map-title" className="sr-only">
        {mapTitle}
      </h1>

      <div className="pointer-events-none absolute inset-0 flex items-center justify-center px-6">
        <p className="max-w-sm text-center text-sm text-text-secondary">
          <fbt desc="Placeholder shown while the map is not loaded">
            La carte s'affichera ici — fond Positron (OpenFreeMap)
          </fbt>
        </p>
      </div>

      <div className="absolute start-4 top-4 flex gap-2">
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
          className={cn(
            "grain absolute start-4 top-16 w-72 rounded-2xl bg-panel-bg p-3 shadow-elevated backdrop-blur",
            showLens && "max-sm:hidden",
          )}
          aria-label={rankingTitle}
        >
          <h2 className="px-2 pt-1 text-sm font-bold">{rankingTitle}</h2>
          <ol className="mt-2 flex flex-col gap-1">
            {SAMPLE_NEIGHBORHOODS.map((result, index) => {
              const selected = result.name === selectedNeighborhood;
              return (
                <li key={result.name}>
                  <button
                    type="button"
                    aria-pressed={selected}
                    onClick={() => onSelectNeighborhood(result.name)}
                    className={rankingRow({ selected })}
                  >
                    <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-highlight text-xs font-bold text-highlight-text">
                      {index + 1}
                    </span>
                    <span className="text-sm font-semibold">{result.name}</span>
                    <span className="text-xs text-text-secondary">{result.district}</span>
                  </button>
                </li>
              );
            })}
          </ol>
        </aside>
      )}

      <Activity mode={showLens ? "visible" : "hidden"}>
        <LensPanel
          criteria={criteria}
          onRemoveCriterion={onRemoveCriterion}
          onClose={() => setShowLens(false)}
        />
      </Activity>

      <div className="absolute inset-x-0 bottom-0 flex flex-wrap items-end justify-between gap-2 p-3 sm:p-4">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-2xl border border-border bg-panel-bg px-3 py-2 text-xs backdrop-blur">
          <span className="flex items-center gap-1.5">
            <fbt desc="Low end of the score ramp legend">Faible</fbt>
            <span className="h-3 w-5 rounded-sm bg-ramp-1 ring-1 ring-border" />
            <span className="h-3 w-5 rounded-sm bg-ramp-2 ring-1 ring-border" />
            <span className="h-3 w-5 rounded-sm bg-ramp-3 ring-1 ring-border" />
            <span className="h-3 w-5 rounded-sm bg-ramp-4 ring-1 ring-border" />
            <span className="h-3 w-5 rounded-sm bg-ramp-5 ring-1 ring-border" />
            <fbt desc="High end of the score ramp legend">Élevé</fbt>
          </span>
          <span className="flex items-center gap-1.5">
            <span className="no-data-pattern h-3 w-5 rounded-sm ring-1 ring-border" />
            <fbt desc="Legend label for neighborhoods without data">Pas de donnée</fbt>
          </span>
        </div>

        <p className="rounded-2xl bg-panel-bg px-3 py-1.5 text-xs text-text-secondary backdrop-blur">
          <fbt desc="Basemap and data attribution shown on the map">
            Fond de carte : OpenFreeMap · Données © OpenStreetMap
          </fbt>
        </p>
      </div>
    </div>
  );
}
