import { describe, expect, it } from "vite-plus/test";
import { ALL_THEME_NAMES, readStoredTheme } from "./theme";

describe("theme names", () => {
  it("exposes eleven unique Istanbul theme ids", () => {
    expect(ALL_THEME_NAMES).toHaveLength(11);
    expect(new Set(ALL_THEME_NAMES).size).toBe(11);
  });

  it("falls back to moda when no window is available", () => {
    expect(readStoredTheme()).toBe("moda");
  });
});
