import { renderToStaticMarkup } from "react-dom/server";
import { setupLocaleContext } from "fbtee";
import { describe, expect, it } from "vite-plus/test";
import { AppShell } from "./app-shell";
import { HomeView } from "../features/home-view/home-view";
import { AVAILABLE_LANGUAGES, SOURCE_LANGUAGE, loadLocale } from "./locale-runtime";

async function renderWithClientLocale(clientLocale: string): Promise<string> {
  const { preloadLocale } = setupLocaleContext({
    availableLanguages: AVAILABLE_LANGUAGES,
    clientLocales: [clientLocale],
    sourceLocale: SOURCE_LANGUAGE,
    loadLocale,
  });
  await preloadLocale();
  return renderToStaticMarkup(
    <AppShell view="home" onNavigate={() => {}}>
      <HomeView onNavigate={() => {}} />
    </AppShell>,
  );
}

describe("interface locale", () => {
  it("renders the interface in English for an English client", async () => {
    const markup = await renderWithClientLocale("en");

    expect(markup).toContain("Find the neighbourhood that fits you.");
    expect(markup).toContain("Map");
    expect(markup).not.toContain("Trouve le quartier");
    expect(markup).not.toContain("Données");
  });

  it("renders the interface in Turkish for a Turkish client", async () => {
    const markup = await renderWithClientLocale("tr");

    expect(markup).toContain("Sana uyan mahalleyi bul.");
    expect(markup).toContain("Harita");
    expect(markup).not.toContain("Trouve le quartier");
    expect(markup).not.toContain("Données");
  });

  it("renders the interface in Arabic for an Arabic client", async () => {
    const markup = await renderWithClientLocale("ar");

    expect(markup).toContain("اعثر على الحي الذي يناسبك.");
    expect(markup).toContain("الخريطة");
    expect(markup).not.toContain("Trouve le quartier");
    expect(markup).not.toContain("Données");
  });
});
