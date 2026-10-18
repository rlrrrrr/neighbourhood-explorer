import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "./locale-runtime";
import { ALL_THEME_NAMES, THEME_DISPLAY_NAMES } from "./theme";
import { ThemeSwitcher } from "./theme-switcher";

describe("theme switcher", () => {
  const markup = renderToStaticMarkup(<ThemeSwitcher theme="moda" onChange={() => {}} />);

  it("lists every theme with the active one pressed", () => {
    for (const theme of ALL_THEME_NAMES) {
      expect(markup).toContain(THEME_DISPLAY_NAMES[theme]);
    }
    expect(markup).toContain('aria-pressed="true"');
    expect(markup.match(/aria-pressed/g)).toHaveLength(ALL_THEME_NAMES.length);
  });

  it("keeps the picker mounted but inert while closed, and reports the collapsed state", () => {
    expect(markup).toContain("anchored-panel");
    expect(markup).toContain('aria-expanded="false"');
    expect(markup).toContain("inert");
    expect(markup).not.toContain("data-open");
  });
});
