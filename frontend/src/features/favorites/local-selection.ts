type AccountStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

const LOCAL_FAVORITES_KEY = "favorites:local:v1";
const MAX_LOCAL_FAVORITES = 100;
const MAX_TARGET_ID_LENGTH = 80;

function browserStorage(): AccountStorage | null {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function uniqueIds(ids: readonly string[]): string[] {
  return [...new Set(ids)];
}

export function readLocalFavoriteIds(
  storage: AccountStorage | null = browserStorage(),
): readonly string[] {
  if (storage === null) {
    return [];
  }

  try {
    const storedValue = storage.getItem(LOCAL_FAVORITES_KEY);
    if (storedValue === null) {
      return [];
    }
    const parsedValue: unknown = JSON.parse(storedValue);
    if (!Array.isArray(parsedValue)) {
      return [];
    }
    const validIds = parsedValue.filter(
      (value): value is string =>
        typeof value === "string" && value.length > 0 && value.length <= MAX_TARGET_ID_LENGTH,
    );
    return uniqueIds(validIds).slice(0, MAX_LOCAL_FAVORITES);
  } catch {
    return [];
  }
}

export function writeLocalFavoriteIds(
  ids: readonly string[],
  storage: AccountStorage | null = browserStorage(),
): void {
  if (storage === null) {
    return;
  }

  try {
    if (ids.length === 0) {
      storage.removeItem(LOCAL_FAVORITES_KEY);
      return;
    }
    storage.setItem(
      LOCAL_FAVORITES_KEY,
      JSON.stringify(uniqueIds(ids).slice(0, MAX_LOCAL_FAVORITES)),
    );
  } catch {
    // The in-memory state still reflects the gesture for the current session.
  }
}

export function mergeFavoriteIds({
  remoteIds,
  localIds,
}: {
  remoteIds: readonly string[];
  localIds: readonly string[];
}): { mergedIds: readonly string[]; uploadIds: readonly string[] } {
  const remote = uniqueIds(remoteIds);
  const remoteSet = new Set(remote);
  const uploadIds = uniqueIds(localIds).filter((targetId) => !remoteSet.has(targetId));
  return { mergedIds: [...remote, ...uploadIds], uploadIds };
}
