import { useEffect } from "react";
import { fbt, type TranslatedString } from "fbtee";
import { entryTitle } from "../features/home-view/entry-copy";
import type { ViewName } from "./url-state";

const PAGE_TITLES: Readonly<Record<ViewName, () => TranslatedString>> = {
  home: () => fbt("Accueil", "Browser tab title of the home page"),
  chat: () => entryTitle("chat"),
  map: () => entryTitle("map"),
  data: () => entryTitle("data"),
};

const PAGE_DESCRIPTIONS: Readonly<Record<ViewName, () => TranslatedString>> = {
  home: () =>
    fbt(
      "Explore les quartiers avec une carte, des critères et des données ouvertes.",
      "Meta description of the home page",
    ),
  chat: () =>
    fbt(
      "Décris le quartier que tu cherches et obtiens des pistes expliquées.",
      "Meta description of the chat view",
    ),
  map: () =>
    fbt(
      "Colorie les quartiers selon une métrique et compare-les sur la carte.",
      "Meta description of the map view",
    ),
  data: () =>
    fbt(
      "Parcours les valeurs brutes, les unités et les sources des quartiers.",
      "Meta description of the data view",
    ),
};

export function useDocumentMeta(view: ViewName): void {
  useEffect(() => {
    document.title = `${PAGE_TITLES[view]()} — Neighbourhood Explorer`;

    let description = document.querySelector<HTMLMetaElement>('meta[name="description"]');

    if (!description) {
      description = document.createElement("meta");
      description.name = "description";
      document.head.append(description);
    }

    description.content = PAGE_DESCRIPTIONS[view]();

    let canonical = document.querySelector<HTMLLinkElement>('link[rel="canonical"]');

    if (!canonical) {
      canonical = document.createElement("link");
      canonical.rel = "canonical";
      document.head.append(canonical);
    }

    canonical.href = `${window.location.origin}${window.location.pathname}`;
  }, [view]);
}
