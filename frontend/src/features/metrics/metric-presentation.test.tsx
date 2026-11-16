import { renderToStaticMarkup } from "react-dom/server";
import { setupLocaleContext } from "fbtee";
import { describe, expect, it } from "vite-plus/test";
import { AVAILABLE_LANGUAGES, SOURCE_LANGUAGE, loadLocale } from "../../app/locale-runtime";
import { localizedUnit, formatMetricValue } from "./metric-presentation";

async function renderUnit({ unit, clientLocale }: { unit: string; clientLocale: string }) {
  const { preloadLocale } = setupLocaleContext({
    availableLanguages: AVAILABLE_LANGUAGES,
    clientLocales: [clientLocale],
    sourceLocale: SOURCE_LANGUAGE,
    loadLocale,
  });
  await preloadLocale();

  function UnitLabel() {
    return <span>{localizedUnit(unit)}</span>;
  }

  return renderToStaticMarkup(<UnitLabel />);
}

describe("localized unit", () => {
  it("renders the French source unit", async () => {
    expect(await renderUnit({ unit: "people/km²", clientLocale: "fr" })).toContain("habitants/km²");
  });

  it("renders the English unit", async () => {
    expect(await renderUnit({ unit: "people/km²", clientLocale: "en" })).toContain("people/km²");
  });

  it("renders the Turkish unit", async () => {
    expect(await renderUnit({ unit: "people/km²", clientLocale: "tr" })).toContain("kişi/km²");
  });

  it("renders the Arabic unit", async () => {
    expect(await renderUnit({ unit: "people/km²", clientLocale: "ar" })).toContain("نسمة/كم²");
  });

  it("keeps an unknown unit unchanged", () => {
    expect(localizedUnit("cafés/km²")).toBe("cafés/km²");
  });
});

describe("metric value formatting", () => {
  it("rounds to the metric display precision", () => {
    expect(formatMetricValue({ value: 8.237491, locale: "fr", maximumFractionDigits: 1 })).toBe(
      "8,2",
    );
  });

  it("never renders a positive value as zero", () => {
    expect(formatMetricValue({ value: 0.0284, locale: "fr", maximumFractionDigits: 1 })).toBe(
      "< 0,1",
    );
  });

  it("keeps an exact zero as zero", () => {
    expect(formatMetricValue({ value: 0, locale: "fr", maximumFractionDigits: 1 })).toBe("0");
  });
});
