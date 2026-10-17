import { fbt, type TranslatedString } from "fbtee";

export type SuggestedCriterion =
  | { kind: "suggested"; key: string; label: () => TranslatedString }
  | { kind: "free-text"; key: string; text: string };

export const SUGGESTED_CRITERIA: readonly SuggestedCriterion[] = [
  {
    kind: "suggested",
    key: "quiet",
    label: () => fbt("Calme", "Sample search criterion chip: quietness"),
  },
  {
    kind: "suggested",
    key: "cafes",
    label: () => fbt("Cafés", "Sample search criterion chip: cafés"),
  },
  {
    kind: "suggested",
    key: "metro",
    label: () => fbt("Métro proche", "Sample search criterion chip: nearby metro"),
  },
];

export function addFreeTextCriterion(
  criteria: readonly SuggestedCriterion[],
  text: string,
): SuggestedCriterion[] {
  const trimmed = text.trim();
  if (trimmed.length === 0) {
    return [...criteria];
  }
  const key = `text:${trimmed}`;
  if (criteria.some((criterion) => criterion.key === key)) {
    return [...criteria];
  }
  return [...criteria, { kind: "free-text", key, text: trimmed }];
}

export function removeCriterion(
  criteria: readonly SuggestedCriterion[],
  key: string,
): SuggestedCriterion[] {
  return criteria.filter((criterion) => criterion.key !== key);
}
