import { describe, expect, it } from "vite-plus/test";
import { addFreeTextCriterion, removeCriterion, SUGGESTED_CRITERIA } from "./chat-criteria";

describe("chat criteria", () => {
  it("appends a free-text criterion without mutating the initial list", () => {
    const next = addFreeTextCriterion(SUGGESTED_CRITERIA, "près d'un parc");

    expect(next).toHaveLength(SUGGESTED_CRITERIA.length + 1);
    expect(next.at(-1)).toEqual({
      kind: "free-text",
      key: "text:près d'un parc",
      text: "près d'un parc",
    });
    expect(SUGGESTED_CRITERIA).toHaveLength(3);
  });

  it("trims the submitted text and ignores a blank submission", () => {
    expect(addFreeTextCriterion(SUGGESTED_CRITERIA, "  calme  ").at(-1)).toMatchObject({
      text: "calme",
    });
    expect(addFreeTextCriterion(SUGGESTED_CRITERIA, "   ")).toEqual([...SUGGESTED_CRITERIA]);
  });

  it("never adds the same criterion twice", () => {
    const once = addFreeTextCriterion(SUGGESTED_CRITERIA, "près du métro");
    const twice = addFreeTextCriterion(once, "près du métro");

    expect(twice).toHaveLength(once.length);
  });

  it("removes a criterion by key and keeps the remaining order", () => {
    const criteria = addFreeTextCriterion(SUGGESTED_CRITERIA, "loin du bruit");

    expect(removeCriterion(criteria, "cafes").map((criterion) => criterion.key)).toEqual([
      "quiet",
      "metro",
      "text:loin du bruit",
    ]);
    expect(removeCriterion(criteria, "unknown")).toHaveLength(criteria.length);
  });
});
