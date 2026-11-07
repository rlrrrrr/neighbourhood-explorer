import { fbt, type TranslatedString } from "fbtee";

export type SampleNeighborhood = {
  neighborhoodId: number;
  name: string;
  district: string;
  cafes: string;
  metro: string;
  green: string;
  sentence: () => TranslatedString;
};

export const SAMPLE_NEIGHBORHOODS: readonly SampleNeighborhood[] = [
  {
    neighborhoodId: 1570,
    name: "Moda",
    district: "Kadıköy",
    cafes: "24,1",
    metro: "8 min",
    green: "31 %",
    sentence: () =>
      fbt(
        "Calme, beaucoup de cafés, métro à moins de 10 minutes.",
        "Sample result sentence for the Moda neighborhood",
      ),
  },
  {
    neighborhoodId: 1272,
    name: "Cihangir",
    district: "Beyoğlu",
    cafes: "18,6",
    metro: "6 min",
    green: "27 %",
    sentence: () =>
      fbt(
        "Animé le soir, très bien desservi, verdure moyenne.",
        "Sample result sentence for the Cihangir neighborhood",
      ),
  },
  {
    neighborhoodId: 1583,
    name: "Yeldeğirmeni",
    district: "Kadıköy",
    cafes: "15,2",
    metro: "11 min",
    green: "22 %",
    sentence: () =>
      fbt(
        "Résidentiel et populaire, cafés en hausse, métro un peu loin.",
        "Sample result sentence for the Yeldeğirmeni neighborhood",
      ),
  },
];
