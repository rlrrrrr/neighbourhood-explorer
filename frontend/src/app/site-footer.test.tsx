import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import { AppLocaleProvider } from "./locale-context";
import { SiteFooter } from "./site-footer";

describe("site footer", () => {
  const markup = renderToStaticMarkup(
    <AppLocaleProvider>
      <SiteFooter />
    </AppLocaleProvider>,
  );

  it("renders eleven blurred gradient columns as a background layer", () => {
    const columns = markup.match(/will-change-transform/g) ?? [];
    expect(columns).toHaveLength(11);
    expect(markup).toContain('aria-hidden="true"');
    expect(markup).toContain("linear-gradient(180deg, var(--highlight)");
    expect(markup).toContain("absolute inset-0");
    expect(markup).toContain("min-h-[max(clamp(360px,52svh,560px),16vw)]");
  });

  it("shows the product name and credits the gradient authors", () => {
    expect(markup).toContain("© 2026");
    expect(markup).toContain("Neighbourhood Explorer");
    expect(markup).toContain("Gradient : Dia gradient, conçu par");
    expect(markup).toContain("Sebastian Dahiya");
    expect(markup).toContain("https://lab.anishfn.ink/dia-gradient");
  });
});
