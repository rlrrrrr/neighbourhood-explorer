import { useLayoutEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { fbs, fbt, type TranslatedString, useLocaleContext } from "fbtee";
import { tv } from "tailwind-variants";
import { useScrollOverflowHint } from "../../components/ui/scroll-affordance";
import { CriteriaChips } from "../criteria/criteria-chips";
import type { Criteria } from "../criteria/criteria";
import type { MetricDefinition } from "../metrics/metric-api-schema";
import { localizedCategory, localizedMetricName } from "../metrics/metric-presentation";
import { filterMetricsByCategory, listMetricCategories } from "./lens-metric-filter";

const lensOption = tv({
  slots: {
    label:
      "flex min-h-11 cursor-pointer items-center gap-2 rounded-full border border-border px-3 text-xs font-semibold text-text transition-[background-color,color] duration-150 ease-out has-[:checked]:bg-highlight has-[:checked]:text-highlight-text has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-action sm:min-h-9 sm:hover:bg-surface",
    indicator:
      "relative flex size-4 shrink-0 items-center justify-center border border-border bg-surface text-action-text transition-colors duration-150 ease-out has-[:checked]:border-action has-[:checked]:bg-action",
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
  value?: string;
  checked: boolean;
  onChange: () => void;
  label: ReactNode;
  shape?: "box" | "dot";
}

function LensOption({
  type,
  name,
  value,
  checked,
  onChange,
  label,
  shape = "box",
}: LensOptionProps) {
  const slots = lensOption({ shape });

  return (
    <label className={slots.label()}>
      <span className={slots.indicator()}>
        <input
          type={type}
          name={name}
          value={value}
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

function LensMetricOptions({
  metrics,
  selectedMetricKey,
  onSelectMetric,
  filterKey,
  locale,
}: {
  metrics: readonly MetricDefinition[];
  selectedMetricKey: string;
  onSelectMetric: (metricKey: string) => void;
  filterKey: string;
  locale: string;
}) {
  const contentRef = useRef<HTMLDivElement | null>(null);
  const [height, setHeight] = useState<number | null>(null);

  useLayoutEffect(() => {
    const content = contentRef.current;

    if (content === null) {
      return;
    }

    const measure = () => setHeight(content.getBoundingClientRect().height);
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(content);

    return () => observer.disconnect();
  }, [filterKey]);

  return (
    <div className="lens-metric-list w-full" style={height === null ? undefined : { height }}>
      <div
        key={filterKey}
        ref={contentRef}
        className="lens-metric-list-content flex flex-wrap gap-1.5"
      >
        {metrics.map((metric) => (
          <LensOption
            key={metric.key}
            type="radio"
            name="lens-metric"
            value={metric.key}
            shape="dot"
            checked={selectedMetricKey === metric.key}
            onChange={() => onSelectMetric(metric.key)}
            label={localizedMetricName(metric, locale)}
          />
        ))}
      </div>
    </div>
  );
}

interface LensPanelProps {
  cityName: string;
  observedAt: string | null;
  criteria: Criteria;
  onRemoveCriterion: (metricKey: string) => void;
  metrics: readonly MetricDefinition[];
  selectedMetricKey: string;
  onSelectMetric: (metricKey: string) => void;
  open: boolean;
  onClose: () => void;
}

export function LensPanel({
  cityName,
  observedAt,
  criteria,
  onRemoveCriterion,
  metrics,
  selectedMetricKey,
  onSelectMetric,
  open,
  onClose,
}: LensPanelProps) {
  const [selectedCategories, setSelectedCategories] = useState<ReadonlySet<string>>(
    () => new Set(),
  );

  const { containerRef, hasMore } = useScrollOverflowHint({ axis: "vertical" });
  const { locale } = useLocaleContext();
  const categories = useMemo(() => listMetricCategories(metrics), [metrics]);

  const visibleMetrics = useMemo(
    () =>
      filterMetricsByCategory({
        metrics,
        selectedCategories,
        currentMetricKey: selectedMetricKey,
      }),
    [metrics, selectedCategories, selectedMetricKey],
  );

  const filterKey = [...selectedCategories].sort().join("|");

  const title = fbt("La lentille", "Title of the lens panel on the map");
  const closeLabel = fbs("Fermer le panneau de la lentille", "Close button of the lens panel");

  return (
    <div className="pointer-events-none absolute inset-0 flex flex-col items-end px-4 pb-52 pt-16 md:pb-16">
      <aside
        data-open={open ? "" : undefined}
        inert={!open}
        className="anchored-panel anchored-panel-origin-start material-panel grain pointer-events-auto flex max-h-full w-72 flex-col overflow-clip rounded-2xl shadow-elevated"
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
              <span className="text-sm font-semibold">{cityName}</span>
            </div>
            <p className="mt-1 text-xs text-text-secondary">
              <fbt desc="Label of the observation date in the lens panel">Date d'observation :</fbt>{" "}
              {observedAt ?? "—"}
            </p>

            {categories.length > 1 && (
              <LensFieldset
                legend={fbt("Catégories", "Legend of the category filter group in the lens panel")}
              >
                {categories.map((category) => (
                  <LensOption
                    key={category}
                    type="checkbox"
                    checked={selectedCategories.has(category)}
                    onChange={() =>
                      setSelectedCategories((current) => toggleKey(current, category))
                    }
                    label={localizedCategory(category)}
                  />
                ))}
              </LensFieldset>
            )}

            {metrics.length > 0 && (
              <LensFieldset
                legend={fbt(
                  "Métrique affichée",
                  "Legend of the metric selector group in the lens panel",
                )}
              >
                <LensMetricOptions
                  metrics={visibleMetrics}
                  selectedMetricKey={selectedMetricKey}
                  onSelectMetric={onSelectMetric}
                  filterKey={filterKey}
                  locale={locale}
                />
              </LensFieldset>
            )}

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
