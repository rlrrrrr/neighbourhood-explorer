import { fbs, fbt, type TranslatedString } from "fbtee";
import { Button } from "../../components/ui/button";
import { FeatheredImage } from "../../components/ui/feathered-image";

type EntryView = "chat" | "map" | "data";

interface HomeViewProps {
  onNavigate: (view: EntryView) => void;
}

const HOME_PHOTOS = [
  {
    key: "ayasofya",
    file: "shifan-hassan-C84cP0Q2j3E-unsplash.jpg",
    credit: "Shifan Hassan · Unsplash",
    alt: () =>
      fbs(
        "Ayasofya encadrée par des fleurs roses, sous un ciel bleu",
        "Alt text of the Ayasofya photo on the home page",
      ),
    caption: () => fbt("Ayasofya", "Caption of the Ayasofya photo on the home page"),
  },
  {
    key: "dolmabahce",
    file: "meric-dagli-kq6undvTxWE-unsplash.jpg",
    credit: "Meric Dagli · Unsplash",
    alt: () =>
      fbs(
        "Le portail du palais de Dolmabahçe ouvre une vue sur le Bosphore",
        "Alt text of the Dolmabahçe photo on the home page",
      ),
    caption: () => fbt("Dolmabahçe", "Caption of the Dolmabahçe photo on the home page"),
  },
  {
    key: "bogaz",
    file: "mehmet-bozgedik-4JilsL1_rrA-unsplash.jpg",
    credit: "Mehmet Bozgedik · Unsplash",
    alt: () =>
      fbs(
        "Panorama du Bosphore et de son pont, depuis les hauteurs",
        "Alt text of the Bosphore photo on the home page",
      ),
    caption: () => fbt("Au-dessus du Bosphore", "Caption of the Bosphore photo on the home page"),
  },
] as const;

const HOME_ENTRY_VIEWS: readonly EntryView[] = ["chat", "map", "data"];

function entryTitle(view: EntryView): TranslatedString {
  switch (view) {
    case "chat":
      return fbt("Chat", "Title of the chat entry card on the home page");
    case "map":
      return fbt("Carte", "Title of the map entry card on the home page");
    case "data":
      return fbt("Données", "Title of the data entry card on the home page");
  }
}

function entryDescription(view: EntryView): TranslatedString {
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

export function HomeView({ onNavigate }: HomeViewProps) {
  return (
    <section className="mx-auto flex w-full max-w-5xl flex-1 flex-col items-center justify-center px-4 py-16 text-center">
      <h1 className="text-4xl font-bold leading-tight tracking-tight text-balance sm:text-6xl">
        <fbt desc="Headline of the home page">Trouve le quartier qui te ressemble.</fbt>
      </h1>
      <p className="mt-6 max-w-xl text-base text-text-secondary text-pretty sm:text-lg">
        <fbt desc="Subline of the home page">
          Une carte, tes critères, des données ouvertes — pour explorer les mahalles de Turquie sans
          score universel.
        </fbt>
      </p>
      <div className="mt-10 flex flex-wrap justify-center gap-3">
        <Button size="lg" onClick={() => onNavigate("map")}>
          <fbt desc="Call to action opening the map view">Explorer la carte</fbt>
        </Button>
        <Button size="lg" variant="outline" onClick={() => onNavigate("chat")}>
          <fbt desc="Call to action opening the chat view">Décrire ce qu'on cherche</fbt>
        </Button>
      </div>
      <ul className="mt-14 grid w-full gap-3 sm:grid-cols-3">
        {HOME_PHOTOS.map((photo) => (
          <li
            key={photo.key}
            className="grain overflow-hidden rounded-2xl bg-surface text-left shadow-elevated"
          >
            <FeatheredImage
              src={`/assets/${photo.file}`}
              alt={photo.alt()}
              loading="lazy"
              decoding="async"
              className="h-44 w-full sm:h-52"
            />
            <p className="px-3 py-2 text-xs text-text-secondary">
              <span className="font-semibold text-text">{photo.caption()}</span>
              <span aria-hidden="true"> · </span>
              {photo.credit}
            </p>
          </li>
        ))}
      </ul>
      <ul className="mt-16 grid w-full gap-3 sm:grid-cols-3">
        {HOME_ENTRY_VIEWS.map((view) => (
          <li key={view}>
            <button
              type="button"
              onClick={() => onNavigate(view)}
              className="grain h-full w-full cursor-pointer rounded-2xl bg-surface p-5 text-left shadow-elevated transition-shadow hover:shadow-elevated-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
            >
              <span className="font-semibold">{entryTitle(view)}</span>
              <span className="mt-1 block text-sm text-text-secondary">
                {entryDescription(view)}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
