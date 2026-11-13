import { describe, expect, it } from "vite-plus/test";
import { mergeFavoriteIds, readLocalFavoriteIds, writeLocalFavoriteIds } from "./local-selection";

function createStorage(): Pick<Storage, "getItem" | "setItem" | "removeItem"> {
  const values = new Map<string, string>();
  return {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => {
      values.set(key, value);
    },
    removeItem: (key) => {
      values.delete(key);
    },
  };
}

describe("local favorites", () => {
  it("starts empty and stores unique target identifiers", () => {
    const storage = createStorage();

    expect(readLocalFavoriteIds(storage)).toEqual([]);

    writeLocalFavoriteIds(["40512", "40512", "40275"], storage);

    expect(readLocalFavoriteIds(storage)).toEqual(["40512", "40275"]);
  });

  it("removes the storage entry when the last favorite is removed", () => {
    const storage = createStorage();
    writeLocalFavoriteIds(["40512"], storage);

    writeLocalFavoriteIds([], storage);

    expect(storage.getItem("favorites:local:v1")).toBeNull();
    expect(readLocalFavoriteIds(storage)).toEqual([]);
  });

  it("ignores malformed or unbounded stored values", () => {
    const storage = createStorage();
    storage.setItem("favorites:local:v1", JSON.stringify(["40512", 42, "", "x".repeat(81)]));

    expect(readLocalFavoriteIds(storage)).toEqual(["40512"]);

    storage.setItem("favorites:local:v1", "{not json");
    expect(readLocalFavoriteIds(storage)).toEqual([]);
  });

  it("merges remote favorites first and uploads only the local missing ones", () => {
    const result = mergeFavoriteIds({
      remoteIds: ["40275", "40512"],
      localIds: ["40512", "40514", "40514"],
    });

    expect(result.mergedIds).toEqual(["40275", "40512", "40514"]);
    expect(result.uploadIds).toEqual(["40514"]);
  });

  it("keeps remote order when every local favorite already exists remotely", () => {
    const result = mergeFavoriteIds({ remoteIds: ["40512", "40275"], localIds: ["40275"] });

    expect(result.mergedIds).toEqual(["40512", "40275"]);
    expect(result.uploadIds).toEqual([]);
  });
});
