import { useState, type ReactNode } from "react";
import { fbs, fbt, type TranslatedString } from "fbtee";
import { tv } from "tailwind-variants";

const lensChip = tv({
  base: "flex min-h-11 cursor-pointer items-center gap-2 rounded-full border border-border px-3 text-xs font-semibold text-text transition-[background-color,color] duration-150 ease-out has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-action sm:min-h-9 sm:hover:bg-surface",
  variants: {
    checked: {
      true: "bg-highlight text-highlight-text",
      false: "bg-transparent",
    },
  },
  defaultVariants: {
    checked: false,
  },
});

const LENS_BASEMAP_STYLES = ["Positron", "Liberty", "Dark"] as const;

const LENS_CATEGORIES = [
  { key: "mobility", label: () => fbt("Mobilité", "Lens filter category: mobility") },
  { key: "local-life", label: () => fbt("Vie locale", "Lens filter category: local life") },
  { key: "nature", label: () => fbt("Nature", "Lens filter category: nature") },
  { key: "services", label: () => fbt("Services", "Lens filter category: services") },
];

const LENS_LAYERS = [
  { key: "metro", label: () => fbt("Métro", "Lens layer: metro") },
  { key: "parks", label: () => fbt("Parcs", "Lens layer: parks") },
  { key: "isochrone", label: () => fbt("Isochrone", "Lens layer: isochrone") },
];

function toggleKey(selected: ReadonlySet<string>, key: string): Set<string> {
  const next = new Set(selected);
  if (next.has(key)) {
    next.delete(key);
  } else {
    next.add(key);
  }
  return next;
}

function LensFieldset({ legend, children }: { legend: TranslatedString; children: ReactNode }) {
  return (
    <fieldset className="mt-5 min-w-0 first:mt-0">
      <legend className="text-xs font-semibold text-text-secondary">{legend}</legend>
      <div className="mt-2 flex flex-wrap gap-1.5">{children}</div>
    </fieldset>
  );
}

interface LensPanelProps {
  onClose: () => void;
}

export function LensPanel({ onClose }: LensPanelProps) {
  const [selectedCategories, setSelectedCategories] = useState<ReadonlySet<string>>(
    () => new Set(),
  );
  const [selectedLayers, setSelectedLayers] = useState<ReadonlySet<string>>(() => new Set());
  const [basemapStyle, setBasemapStyle] =
    useState<(typeof LENS_BASEMAP_STYLES)[number]>("Positron");
  const title = fbt("La lentille", "Title of the lens panel on the map");
  const closeLabel = fbs("Fermer le panneau de la lentille", "Close button of the lens panel");

  return (
    <aside
      className="grain absolute right-4 top-16 flex max-h-[calc(100%-8rem)] w-72 flex-col overflow-hidden rounded-2xl bg-panel-bg shadow-elevated backdrop-blur"
      aria-label={title}
    >
      <div className="flex items-center justify-between gap-2 border-b border-border px-4 py-3">
        <h2 className="text-sm font-bold">{title}</h2>
        <button
          type="button"
          aria-label={closeLabel}
          onClick={onClose}
          className="flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-full text-text-secondary transition-colors duration-150 ease-out hover:bg-surface hover:text-text focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
        >
          <svg
            viewBox="0 0 16 16"
            className="size-4"
            aria-hidden="true"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.5"
            strokeLinecap="round"
          >
            <path d="M4 4l8 8M12 4l-8 8" />
          </svg>
        </button>
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-4">
        <div className="flex items-baseline justify-between">
          <span className="text-xs font-semibold text-text-secondary">
            <fbt desc="Label of the city field in the lens panel">Ville</fbt>
          </span>
          <span className="text-sm font-semibold">Istanbul</span>
        </div>
        <p className="mt-1 text-xs text-text-secondary">
          <fbt desc="Note explaining that no sample data has a time dimension">
            Période masquée : aucune donnée en cours n'a de dimension temporelle
          </fbt>
        </p>

        <LensFieldset
          legend={fbt("Catégories", "Legend of the category filter group in the lens panel")}
        >
          {LENS_CATEGORIES.map((category) => (
            <label
              key={category.key}
              className={lensChip({ checked: selectedCategories.has(category.key) })}
            >
              <input
                type="checkbox"
                className="size-4 shrink-0 cursor-pointer accent-action"
                checked={selectedCategories.has(category.key)}
                onChange={() =>
                  setSelectedCategories((current) => toggleKey(current, category.key))
                }
              />
              {category.label()}
            </label>
          ))}
        </LensFieldset>

        <section className="mt-5" aria-labelledby="lens-selection-label">
          <h3 id="lens-selection-label" className="text-xs font-semibold text-text-secondary">
            <fbt desc="Label of the saved selection field in the lens panel">Sélection</fbt>
          </h3>
          <p className="mt-2 rounded-xl bg-surface px-3 py-2 text-xs text-text-secondary">
            <fbt desc="Empty state of the saved selection in the lens panel">
              Aucun quartier enregistré pour l'instant
            </fbt>
          </p>
        </section>

        <LensFieldset
          legend={fbt("Fond de carte", "Legend of the basemap style group in the lens panel")}
        >
          {LENS_BASEMAP_STYLES.map((style) => (
            <label key={style} className={lensChip({ checked: basemapStyle === style })}>
              <input
                type="radio"
                name="lens-basemap"
                className="size-4 shrink-0 cursor-pointer accent-action"
                checked={basemapStyle === style}
                onChange={() => setBasemapStyle(style)}
              />
              {style}
            </label>
          ))}
        </LensFieldset>

        <LensFieldset legend={fbt("Couches", "Legend of the layer group in the lens panel")}>
          {LENS_LAYERS.map((layer) => (
            <label key={layer.key} className={lensChip({ checked: selectedLayers.has(layer.key) })}>
              <input
                type="checkbox"
                className="size-4 shrink-0 cursor-pointer accent-action"
                checked={selectedLayers.has(layer.key)}
                onChange={() => setSelectedLayers((current) => toggleKey(current, layer.key))}
              />
              {layer.label()}
            </label>
          ))}
        </LensFieldset>
      </div>
    </aside>
  );
}
