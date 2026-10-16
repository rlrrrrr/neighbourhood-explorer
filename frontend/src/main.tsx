import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import App from "./App.tsx";
import { AppLocaleProvider } from "./app/locale-context";
import { LocaleContext } from "./app/locale-runtime";

await LocaleContext.preload();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppLocaleProvider>
      <App />
    </AppLocaleProvider>
  </StrictMode>,
);
