import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import App from "./App.tsx";
import { AppLocaleProvider } from "./app/locale-context";
import { LocaleContext } from "./app/locale-runtime";
import { applyStoredTheme } from "./app/theme";

await LocaleContext.preload();

applyStoredTheme();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppLocaleProvider>
      <App />
    </AppLocaleProvider>
  </StrictMode>,
);
