import { fbt, type TranslatedString } from "fbtee";

export interface SampleMetric {
  key: string;
  label: () => TranslatedString;
}

export const SAMPLE_METRICS: readonly SampleMetric[] = [
  {
    key: "quiet",
    label: () => fbt("Calme", "Sample search criterion chip: quietness"),
  },
  {
    key: "cafes",
    label: () => fbt("Cafés", "Sample search criterion chip: cafés"),
  },
  {
    key: "metro",
    label: () => fbt("Métro proche", "Sample search criterion chip: nearby metro"),
  },
];

export function metricLabel(metricKey: string): TranslatedString | undefined {
  return SAMPLE_METRICS.find((metric) => metric.key === metricKey)?.label();
}
