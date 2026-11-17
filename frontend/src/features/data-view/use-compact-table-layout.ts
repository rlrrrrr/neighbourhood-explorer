import { useSyncExternalStore } from "react";

const COMPACT_TABLE_QUERY = "(max-width: 767px)";

function subscribeToViewport(onChange: () => void): () => void {
  const mediaQuery = window.matchMedia(COMPACT_TABLE_QUERY);
  mediaQuery.addEventListener("change", onChange);

  return () => mediaQuery.removeEventListener("change", onChange);
}

function isCompactViewport(): boolean {
  return window.matchMedia(COMPACT_TABLE_QUERY).matches;
}

export function useCompactTableLayout(): boolean {
  return useSyncExternalStore(subscribeToViewport, isCompactViewport, () => false);
}
