import { fbs, fbt, type TranslatedString } from "fbtee";
import { Button } from "../../components/ui/button";
import {
  SAMPLE_NEIGHBORHOODS,
  type SampleNeighborhood,
} from "../neighborhood-detail/sample-neighborhoods";

interface ChatViewProps {
  onShowOnMap: () => void;
}

const SAMPLE_CRITERIA = [
  { key: "quiet", label: () => fbt("Calme", "Sample search criterion chip: quietness") },
  { key: "cafes", label: () => fbt("Cafés", "Sample search criterion chip: cafés") },
  { key: "metro", label: () => fbt("Métro proche", "Sample search criterion chip: nearby metro") },
];

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
  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight text-balance">
        <fbt desc="Title of the chat view, above the input">Décris le quartier que tu cherches</fbt>
      </h1>
      <div className="mt-6 flex gap-2">
        <input
          id="chat-command"
          className="min-h-11 flex-1 rounded-full border border-border bg-surface px-5 text-sm outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
          type="text"
          placeholder={fbs(
            "Ex. un quartier calme avec des cafés, près du métro",
            "Placeholder of the chat input, with an example search",
          )}
        />
        <Button type="button">
          <fbt desc="Label of the chat send button">Envoyer</fbt>
        </Button>
      </div>
      <ul className="mt-4 flex flex-wrap gap-2">
        {SAMPLE_CRITERIA.map((criterion) => (
          <li
            key={criterion.key}
            className="rounded-full border border-border bg-highlight px-3 py-1 text-xs font-semibold text-highlight-text"
          >
            {criterion.label()}
          </li>
        ))}
      </ul>
      <p className="mt-4 text-xs text-text-secondary">
        <fbt desc="Note explaining that the chat input will be wired to the preference engine">
          Aperçu du thème : la saisie sera reliée au moteur de préférences. Sans IA, les critères
          manuels et la recherche par nom prennent le relais.
        </fbt>
      </p>

      <ul className="mt-12 flex flex-col gap-4">
        {SAMPLE_NEIGHBORHOODS.map((result, index) => (
          <li
            key={result.name}
            className={
              index === 0
                ? "grain rounded-2xl bg-surface p-5 shadow-elevated ring-2 ring-highlight"
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
            <Button variant="outline" size="sm" className="mt-5" onClick={onShowOnMap}>
              <fbt desc="Button opening the map view for a sample result">Voir sur la carte</fbt>
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}
