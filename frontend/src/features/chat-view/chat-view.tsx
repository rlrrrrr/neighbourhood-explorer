import { useState, type ReactNode } from "react";
import { fbs, fbt, type TranslatedString } from "fbtee";
import { Button } from "../../components/ui/button";
import { ChatComposer } from "./chat-composer";
import {
  addFreeTextCriterion,
  removeCriterion,
  SUGGESTED_CRITERIA,
  type SuggestedCriterion,
} from "./chat-criteria";
import {
  SAMPLE_NEIGHBORHOODS,
  type SampleNeighborhood,
} from "../neighborhood-detail/sample-neighborhoods";

interface ChatViewProps {
  onShowOnMap: (neighborhood: string) => void;
}

function criterionLabel(criterion: SuggestedCriterion): ReactNode {
  return criterion.kind === "suggested" ? criterion.label() : criterion.text;
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

export function ChatView({ onShowOnMap }: ChatViewProps) {
  const [criteria, setCriteria] = useState<readonly SuggestedCriterion[]>(SUGGESTED_CRITERIA);

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight text-balance">
        <fbt desc="Title of the chat view, above the input">Décris le quartier que tu cherches</fbt>
      </h1>
      <div className="mt-6">
        <ChatComposer
          onSubmit={(text) => setCriteria((current) => addFreeTextCriterion(current, text))}
        />
      </div>
      {criteria.length === 0 ? (
        <p className="mt-4 text-xs text-text-secondary">
          <fbt desc="Empty state shown when every criterion chip has been removed">
            Aucun critère pour l'instant : décris ce que tu cherches pour en ajouter.
          </fbt>
        </p>
      ) : (
        <ul className="mt-4 flex flex-wrap gap-2">
          {criteria.map((criterion) => (
            <li
              key={criterion.key}
              className="flex min-h-9 items-center gap-1 rounded-full border border-border bg-highlight pr-1 pl-3 text-xs font-semibold text-highlight-text"
            >
              {criterionLabel(criterion)}
              <button
                type="button"
                aria-label={fbs(
                  "Retirer ce critère",
                  "Label of the button removing a search criterion chip",
                )}
                onClick={() => setCriteria((current) => removeCriterion(current, criterion.key))}
                className="flex size-7 cursor-pointer items-center justify-center rounded-full transition-colors duration-150 ease-out hover:bg-highlight-text/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
              >
                <svg
                  viewBox="0 0 16 16"
                  className="size-3"
                  aria-hidden="true"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                >
                  <path d="M4 4l8 8M12 4l-8 8" />
                </svg>
              </button>
            </li>
          ))}
        </ul>
      )}

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
