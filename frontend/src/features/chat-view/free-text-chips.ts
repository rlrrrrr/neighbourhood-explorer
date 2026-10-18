export interface FreeTextChip {
  key: string;
  text: string;
}

export function addFreeTextChip(
  chips: readonly FreeTextChip[],
  text: string,
): readonly FreeTextChip[] {
  const trimmed = text.trim();
  if (trimmed.length === 0) {
    return chips;
  }
  const key = `text:${trimmed}`;
  if (chips.some((chip) => chip.key === key)) {
    return chips;
  }
  return [...chips, { key, text: trimmed }];
}

export function removeFreeTextChip(
  chips: readonly FreeTextChip[],
  key: string,
): readonly FreeTextChip[] {
  const remaining = chips.filter((chip) => chip.key !== key);
  return remaining.length === chips.length ? chips : remaining;
}
