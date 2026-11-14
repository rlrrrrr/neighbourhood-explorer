import { fbt, type TranslatedString } from "fbtee";
import type { MetricDefinition, MetricValue } from "./metric-api-schema";

type QualityFlag = MetricValue["qualityFlag"];

const KNOWN_UNITS: Readonly<Record<string, () => TranslatedString>> = {
  "people/km²": () =>
    fbt("habitants/km²", "Unit of population density, people per square kilometre"),
};

const KNOWN_CATEGORIES: Readonly<Record<string, () => TranslatedString>> = {
  Lifestyle: () => fbt("Vie locale", "Metric category: shops, cafés, culture and nightlife"),
  Education: () => fbt("Éducation", "Metric category: schools and education"),
  Environment: () => fbt("Environnement", "Metric category: greenery and environment"),
  Health: () => fbt("Santé", "Metric category: healthcare"),
  Urban: () => fbt("Forme urbaine", "Metric category: urban form and land use"),
  People: () => fbt("Population", "Metric category: population"),
  Mobility: () => fbt("Mobilité", "Metric category: transport and walkability"),
};

export function localizedUnit(unit: string): string {
  return KNOWN_UNITS[unit]?.() ?? unit;
}

export function localizedCategory(category: string): string {
  return KNOWN_CATEGORIES[category]?.() ?? category;
}

export function localizedMetricName(metric: MetricDefinition, locale: string): string {
  const translation =
    metric.translations.find((candidate) => candidate.locale === locale) ??
    metric.translations.find((candidate) => candidate.locale === "fr") ??
    metric.translations[0];
  return translation?.name ?? metric.key;
}

export function formatMetricValue({
  value,
  locale,
  maximumFractionDigits,
}: {
  value: number;
  locale: string;
  maximumFractionDigits: number;
}): string {
  return new Intl.NumberFormat(locale, { maximumFractionDigits }).format(value);
}

export function shortLicense(license: string): string {
  return license.split(/\s+[—–-]\s+/)[0].trim();
}

export function metricQualityLabel(flag: QualityFlag): TranslatedString {
  switch (flag) {
    case "inherited":
      return fbt("Valeur du district", "Quality label for a value inherited from the district");
    case "estimated":
      return fbt("Estimation", "Quality label for an estimated value");
    case "stale":
      return fbt("Donnée ancienne", "Quality label for a stale value");
    case "low_coverage":
      return fbt("Couverture faible", "Quality label for a value with low coverage");
    case "ok":
      return fbt("Mesure", "Quality label for an observed value");
  }
}
