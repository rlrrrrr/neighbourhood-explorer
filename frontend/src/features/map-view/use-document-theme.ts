import { useEffect, useState } from "react";
import { readCurrentTheme, type ThemeName } from "../../app/theme";

export function useDocumentTheme(): ThemeName {
  const [theme, setTheme] = useState<ThemeName>(() => readCurrentTheme());

  useEffect(() => {
    const update = () => setTheme(readCurrentTheme());
    update();

    const observer = new MutationObserver(update);
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ["data-theme"],
    });

    return () => observer.disconnect();
  }, []);

  return theme;
}
