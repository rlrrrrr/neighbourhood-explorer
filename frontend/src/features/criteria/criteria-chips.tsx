import { fbs } from "fbtee";
import { ChipList, type Chip } from "../../components/ui/chip-list";
import type { Criteria } from "./criteria";
import { metricLabel } from "./sample-metrics";

interface CriteriaChipsProps {
  criteria: Criteria;
  onRemove: (metricKey: string) => void;
}

export function CriteriaChips({ criteria, onRemove }: CriteriaChipsProps) {
  const chips: Chip[] = criteria.weights.map((criterion) => ({
    key: criterion.metricKey,
    label: metricLabel(criterion.metricKey) ?? criterion.metricKey,
  }));

  return (
    <ChipList
      chips={chips}
      removeLabel={fbs(
        "Retirer ce critère",
        "Label of the button removing a search criterion chip",
      )}
      onRemove={onRemove}
    />
  );
}
