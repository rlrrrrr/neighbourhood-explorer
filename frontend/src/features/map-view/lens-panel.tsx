import { useState, type ReactNode } from "react";
import { fbs, fbt, type TranslatedString } from "fbtee";
import { tv } from "tailwind-variants";
import { useScrollOverflowHint } from "../../components/ui/scroll-affordance";
import { CriteriaChips } from "../criteria/criteria-chips";
import type { Criteria } from "../criteria/criteria";

const lensOption = tv({
  slots: {
    label:
      "flex min-h-11 cursor-pointer items-center gap-2 rounded-full border border-border px-3 text-xs font-semibold text-text transition-[background-color,color] duration-150 ease-out has-[:checked]:bg-highlight has-[:checked]:text-highlight-text has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-action sm:min-h-9 sm:hover:bg-surface",
    indicator:
      "flex size-4 shrink-0 items-center justify-center border border-border bg-surface text-action-text transition-colors duration-150 ease-out has-[:checked]:border-action has-[:checked]:bg-action",
    glyph:
      "block size-2.5 opacity-0 transition-opacity duration-150 ease-out peer-checked:opacity-100",
    dot: "size-1.5 rounded-full bg-action-text opacity-0 transition-opacity duration-150 ease-out peer-checked:opacity-100",
  },
  variants: {
    shape: {
      box: { indicator: "rounded-[0.25rem]" },
      dot: { indicator: "rounded-full" },
    },
  },
  defaultVariants: {
    shape: "box",
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

interface LensOptionProps {
  type: "checkbox" | "radio";
  name?: string;
  checked: boolean;
  onChange: () => void;
  label: ReactNode;
  shape?: "box" | "dot";
}

function LensOption({ type, name, checked, onChange, label, shape = "box" }: LensOptionProps) {
  const slots = lensOption({ shape });

  return (
    <label className={slots.label()}>
      <span className={slots.indicator()}>
        <input
          type={type}
          name={name}
          className="peer sr-only"
          checked={checked}
          onChange={onChange}
        />
        {shape === "box" ? (
          <span aria-hidden="true" className={slots.glyph()}>
            <svg
              viewBox="0 0 12 12"
              className="size-full"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M2 6.5 4.5 9 10 3" />
            </svg>
          </span>
        ) : (
          <span aria-hidden="true" className={slots.dot()} />
        )}
      </span>
      {label}
    </label>
  );
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
  criteria: Criteria;
  onRemoveCriterion: (metricKey: string) => void;
  open: boolean;
  onClose: () => void;
}

export function LensPanel({ criteria, onRemoveCriterion, open, onClose }: LensPanelProps) {
  const [selectedCategories, setSelectedCategories] = useState<ReadonlySet<string>>(
    () => new Set(),
  );
  const [selectedLayers, setSelectedLayers] = useState<ReadonlySet<string>>(() => new Set());
  const [basemapStyle, setBasemapStyle] =
    useState<(typeof LENS_BASEMAP_STYLES)[number]>("Positron");
  const { containerRef, hasMore } = useScrollOverflowHint({ axis: "vertical" });
  const title = fbt("La lentille", "Title of the lens panel on the map");
  const closeLabel = fbs("Fermer le panneau de la lentille", "Close button of the lens panel");

  return (
    <div className="pointer-events-none absolute inset-0 flex flex-col items-end px-4 pb-28 pt-16 sm:pb-16">
      <aside
        data-open={open ? "" : undefined}
        inert={!open}
        className="anchored-panel anchored-panel-origin-start material-panel grain pointer-events-auto flex max-h-full w-72 flex-col overflow-hidden rounded-2xl shadow-elevated"
        aria-label={title}
      >
        <div className="flex items-center justify-between gap-2 border-b border-border px-4 py-3">
          <h2 className="text-sm font-bold">{title}</h2>
          <button
            type="button"
            aria-label={closeLabel}
            onClick={onClose}
            className="flex size-11 shrink-0 cursor-pointer items-center justify-center rounded-full text-text-secondary transition-colors duration-150 ease-out hover:bg-surface hover:text-text focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action sm:size-8"
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

        <div className="relative min-h-0 flex-1">
          <div ref={containerRef} className="h-full overflow-y-auto px-4 pb-8 pt-4">
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
                <LensOption
                  key={category.key}
                  type="checkbox"
                  checked={selectedCategories.has(category.key)}
                  onChange={() =>
                    setSelectedCategories((current) => toggleKey(current, category.key))
                  }
                  label={category.label()}
                />
              ))}
            </LensFieldset>

            <section className="mt-5" aria-labelledby="lens-criteria-label">
              <h3 id="lens-criteria-label" className="text-xs font-semibold text-text-secondary">
                <fbt desc="Legend of the active criteria group in the lens panel">Critères</fbt>
              </h3>
              {criteria.weights.length === 0 ? (
                <p className="mt-2 rounded-xl bg-surface px-3 py-2 text-xs text-text-secondary">
                  <fbt desc="Empty state of the active criteria group in the lens panel">
                    Aucun critère actif : ajoute-les depuis le chat.
                  </fbt>
                </p>
              ) : (
                <div className="mt-2">
                  <CriteriaChips criteria={criteria} onRemove={onRemoveCriterion} />
                </div>
              )}
            </section>

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
                <LensOption
                  key={style}
                  type="radio"
                  name="lens-basemap"
                  shape="dot"
                  checked={basemapStyle === style}
                  onChange={() => setBasemapStyle(style)}
                  label={style}
                />
              ))}
            </LensFieldset>

            <LensFieldset legend={fbt("Couches", "Legend of the layer group in the lens panel")}>
              {LENS_LAYERS.map((layer) => (
                <LensOption
                  key={layer.key}
                  type="checkbox"
                  checked={selectedLayers.has(layer.key)}
                  onChange={() => setSelectedLayers((current) => toggleKey(current, layer.key))}
                  label={layer.label()}
                />
              ))}
            </LensFieldset>
          </div>

          {hasMore && (
            <div
              aria-hidden="true"
              className="pointer-events-none absolute inset-x-0 bottom-0 h-10 rounded-b-2xl bg-gradient-to-t from-panel-bg to-transparent"
            />
          )}
        </div>
      </aside>
    </div>
  );
}
