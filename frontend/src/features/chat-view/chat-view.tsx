import { useState } from "react";
import { fbs, fbt, type TranslatedString } from "fbtee";
import { Button } from "../../components/ui/button";
import { ChipList, type Chip } from "../../components/ui/chip-list";
import { ChatComposer } from "./chat-composer";
import { addFreeTextChip, removeFreeTextChip, type FreeTextChip } from "./free-text-chips";
import { hasCriterion, type Criteria } from "../criteria/criteria";
import { SAMPLE_METRICS } from "../criteria/sample-metrics";
import {
  SAMPLE_NEIGHBORHOODS,
  type SampleNeighborhood,
} from "../neighborhood-detail/sample-neighborhoods";

interface ChatViewProps {
  criteria: Criteria;
  onToggleCriterion: (metricKey: string) => void;
  onShowOnMap: (neighborhood: string) => void;
}

function resultStats(
  neighborhood: SampleNeighborhood,
): Array<{ key: string; label: TranslatedString; value: string }> {
  return [
    {
      key: "cafes",
      label: fbt("Cafés / km²", "Label of the café density stat on a sample result"),
      value: neighborhood.cafes,
    },
    {
      key: "metro",
      label: fbt("Métro à pied", "Label of the walking distance to the metro stat"),
      value: neighborhood.metro,
    },
    {
      key: "green",
      label: fbt("Verdure", "Label of the green cover stat on a sample result"),
      value: neighborhood.green,
    },
  ];
}

export function ChatView({ criteria, onToggleCriterion, onShowOnMap }: ChatViewProps) {
  const [freeTextChips, setFreeTextChips] = useState<readonly FreeTextChip[]>([]);
  const freeTextChipList: Chip[] = freeTextChips.map((chip) => ({
    key: chip.key,
    label: chip.text,
  }));
  const removeLabel = fbs(
    "Retirer ce critère",
    "Label of the button removing a search criterion chip",
  );

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight text-balance">
        <fbt desc="Title of the chat view, above the input">Décris le quartier que tu cherches</fbt>
      </h1>
      <div className="mt-6">
        <ChatComposer
          onSubmit={(text) => setFreeTextChips((current) => addFreeTextChip(current, text))}
        />
      </div>

      {freeTextChips.length === 0 && criteria.weights.length === 0 && (
        <p className="mt-4 text-xs text-text-secondary">
          <fbt desc="Empty state shown when every criterion chip has been removed">
            Aucun critère pour l'instant : décris ce que tu cherches pour en ajouter.
          </fbt>
        </p>
      )}

      {freeTextChips.length > 0 && (
        <ChipList
          className="mt-4"
          chips={freeTextChipList}
          removeLabel={removeLabel}
          onRemove={(key) => setFreeTextChips((current) => removeFreeTextChip(current, key))}
        />
      )}

      <fieldset className="mt-6 min-w-0">
        <legend className="text-xs font-semibold text-text-secondary">
          <fbt desc="Legend of the suggested criterion buttons in the chat view">
            Ajouter un critère
          </fbt>
        </legend>
        <ul className="mt-2 flex flex-wrap gap-2">
          {SAMPLE_METRICS.map((metric) => {
            const selected = hasCriterion(criteria, metric.key);
            return (
              <li key={metric.key}>
                <Button
                  size="sm"
                  variant={selected ? "primary" : "outline"}
                  aria-pressed={selected}
                  onClick={() => onToggleCriterion(metric.key)}
                >
                  {metric.label()}
                </Button>
              </li>
            );
          })}
        </ul>
      </fieldset>

      <ul className="mt-12 flex flex-col gap-4">
        {SAMPLE_NEIGHBORHOODS.map((result, index) => (
          <li
            key={result.name}
            className={
              index === 0
                ? "grain rounded-2xl bg-surface p-5 shadow-elevated ring-2 ring-action/60"
                : "grain rounded-2xl bg-surface p-5 shadow-elevated"
            }
          >
            <div className="flex items-center gap-3">
              <span className="text-lg font-bold">{result.name}</span>
              <span className="text-sm text-text-secondary">{result.district}</span>
              {index === 0 && (
                <span className="ml-auto rounded-full bg-highlight px-3 py-0.5 text-xs font-semibold text-highlight-text">
                  <fbt desc="Badge shown on the first sample result">Meilleur résultat</fbt>
                </span>
              )}
            </div>
            <p className="mt-2 text-sm text-text-secondary">{result.sentence()}</p>
            <ul className="mt-4 flex flex-wrap gap-x-6 gap-y-2">
              {resultStats(result).map((stat) => (
                <li key={stat.key} className="flex items-baseline gap-2">
                  <span className="font-semibold tabular-nums">{stat.value}</span>
                  <span className="text-xs text-text-secondary">{stat.label}</span>
                </li>
              ))}
            </ul>
            <Button
              variant="outline"
              size="sm"
              className="mt-5"
              onClick={() => onShowOnMap(result.name)}
            >
              <fbt desc="Button opening the map view for a sample result">Voir sur la carte</fbt>
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}
