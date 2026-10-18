import { describe, expect, it } from "vite-plus/test";
import { addFreeTextChip, removeFreeTextChip, type FreeTextChip } from "./free-text-chips";

const CHIPS: readonly FreeTextChip[] = [{ key: "text:calme", text: "calme" }];

describe("free-text chips", () => {
  it("appends the trimmed submitted text without mutating the current list", () => {
    const next = addFreeTextChip(CHIPS, "  près d'un parc  ");

    expect(next).toEqual([
      { key: "text:calme", text: "calme" },
      { key: "text:près d'un parc", text: "près d'un parc" },
    ]);
    expect(CHIPS).toHaveLength(1);
  });

  it("ignores a blank submission and never adds the same text twice", () => {
    expect(addFreeTextChip(CHIPS, "   ")).toBe(CHIPS);
    expect(addFreeTextChip(CHIPS, "calme")).toBe(CHIPS);
  });

  it("removes a chip by key and keeps the list identical for an unknown key", () => {
    expect(removeFreeTextChip(CHIPS, "text:calme")).toEqual([]);
    expect(removeFreeTextChip(CHIPS, "text:inconnu")).toBe(CHIPS);
  });
});
