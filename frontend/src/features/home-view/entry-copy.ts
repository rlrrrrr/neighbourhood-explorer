import { fbt, type TranslatedString } from "fbtee";

export type EntryView = "chat" | "map" | "data";

export function entryTitle(view: EntryView): TranslatedString {
  switch (view) {
    case "chat":
      return fbt("Chat", "Title of the chat entry card on the home page");
    case "map":
      return fbt("Carte", "Title of the map entry card on the home page");
    case "data":
      return fbt("Données", "Title of the data entry card on the home page");
  }
}

export function entryDescription(view: EntryView): TranslatedString {
  switch (view) {
    case "chat":
      return fbt(
        "Écris ce que tu cherches, reçois des pistes expliquées.",
        "Description of the chat entry card on the home page",
      );
    case "map":
      return fbt(
        "Les quartiers colorés selon tes propres critères.",
        "Description of the map entry card on the home page",
      );
    case "data":
      return fbt(
        "Valeurs brutes, unités et sources, colonne par colonne.",
        "Description of the data entry card on the home page",
      );
  }
}
