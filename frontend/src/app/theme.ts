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

const THEME_STORAGE_KEY = "theme:v1";

function isThemeName(value: string | null): value is ThemeName {
  return ALL_THEME_NAMES.includes(value as ThemeName);
}

export function readStoredTheme(): ThemeName {
  if (typeof window === "undefined") {
    return "moda";
  }
  try {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
    return isThemeName(stored) ? stored : "moda";
  } catch {
    // `localStorage` peut lever (navigation privée, stockage désactivé) : on retombe sur le thème par défaut.
    return "moda";
  }
}

export function applyTheme(theme: ThemeName): void {
  const root = document.documentElement;
  root.classList.add("theme-switching");
  root.dataset.theme = theme;
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // Même raison : le thème s'applique quand même pour la session en cours.
  }
  window.requestAnimationFrame(() => {
    window.requestAnimationFrame(() => {
      root.classList.remove("theme-switching");
    });
  });
}
