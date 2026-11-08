import { fbt, type TranslatedString } from "fbtee";

const KNOWN_UNITS: Readonly<Record<string, () => TranslatedString>> = {
  "people/km²": () =>
    fbt("habitants/km²", "Unit of population density, people per square kilometre"),
};

export function localizedUnit(unit: string): string {
  return KNOWN_UNITS[unit]?.() ?? unit;
}
