import { describe, expect, it } from "vite-plus/test";
import { toAccountLocale } from "./account-locale";

describe("account locale", () => {
  it("keeps the locales the accounts API accepts", () => {
    expect(toAccountLocale("fr")).toBe("fr");
    expect(toAccountLocale("en")).toBe("en");
    expect(toAccountLocale("tr")).toBe("tr");
    expect(toAccountLocale("ar")).toBe("ar");
  });

  it("does not forward an unknown locale", () => {
    expect(toAccountLocale("de")).toBeNull();
    expect(toAccountLocale("")).toBeNull();
  });
});
