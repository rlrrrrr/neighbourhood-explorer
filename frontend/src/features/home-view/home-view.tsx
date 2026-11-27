import { fbs, fbt } from "fbtee";
import { Button } from "../../components/ui/button";
import { FeatheredImage } from "../../components/ui/feathered-image";
import { entryDescription, entryTitle, type EntryView } from "./entry-copy";

interface HomeViewProps {
  onNavigate: (view: EntryView) => void;
}

const HOME_PHOTOS = [
  {
    key: "ayasofya",
    file: "shifan-hassan-C84cP0Q2j3E-unsplash.webp",
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
    file: "meric-dagli-kq6undvTxWE-unsplash.webp",
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
    file: "mehmet-bozgedik-4JilsL1_rrA-unsplash.webp",
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
      </div>
      <ul className="mt-14 grid w-full gap-3 sm:grid-cols-3">
        {HOME_ENTRY_VIEWS.map((view) => (
          <li key={view}>
            <button
              type="button"
              onClick={() => onNavigate(view)}
              className="grain h-full w-full cursor-pointer rounded-2xl bg-surface p-5 text-left shadow-elevated transition-shadow hover:shadow-elevated-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action motion-safe:active:scale-[0.98]"
            >
              <span className="font-semibold">{entryTitle(view)}</span>
              <span className="mt-1 block text-sm text-text-secondary">
                {entryDescription(view)}
              </span>
            </button>
          </li>
        ))}
      </ul>
      <ul className="mt-16 grid w-full gap-3 sm:grid-cols-3">
        {HOME_PHOTOS.map((photo, index) => (
          <li
            key={photo.key}
            className="grain overflow-hidden rounded-2xl bg-surface text-left shadow-elevated"
          >
            <FeatheredImage
              src={`/assets/${photo.file}`}
              alt={photo.alt()}
              loading={index === 0 ? "eager" : "lazy"}
              fetchPriority={index === 0 ? "high" : "auto"}
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
    </section>
  );
}
