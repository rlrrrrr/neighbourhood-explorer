export const ALL_THEME_NAMES = [
  "moda",
  "gunes",
  "lale",
  "gul",
  "sabah",
  "kandil",
  "carsi",
  "yeditepe",
  "fener",
  "sis",
  "gece",
] as const;

export type ThemeName = (typeof ALL_THEME_NAMES)[number];

export const THEME_DISPLAY_NAMES: Record<ThemeName, string> = {
  moda: "Moda",
  gunes: "Güneş",
  lale: "Lale",
  gul: "Gül",
  sabah: "Sabah",
  kandil: "Kandil",
  carsi: "Çarşı",
  yeditepe: "Yeditepe",
  fener: "Fener",
  sis: "Sis",
  gece: "Gece",
};

const THEME_STORAGE_KEY = "theme";

function isThemeName(value: string | null): value is ThemeName {
  return ALL_THEME_NAMES.includes(value as ThemeName);
}

export function readStoredTheme(): ThemeName {
  if (typeof window === "undefined") {
    return "moda";
  }
  const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
  return isThemeName(stored) ? stored : "moda";
}

export function applyTheme(theme: ThemeName): void {
  const root = document.documentElement;
  root.classList.add("theme-switching");
  root.dataset.theme = theme;
  window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  window.requestAnimationFrame(() => {
    window.requestAnimationFrame(() => {
      root.classList.remove("theme-switching");
    });
  });
}
