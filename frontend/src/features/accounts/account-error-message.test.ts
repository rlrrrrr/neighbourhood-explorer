import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { accountErrorMessage, validationErrorMessage } from "./account-error-message";

describe("account error messages", () => {
  it("explains invalid credentials without revealing which field is wrong", () => {
    expect(accountErrorMessage({ code: "invalid_credentials", errors: [] })).toBe(
      "Courriel ou mot de passe incorrect.",
    );
  });

  it("maps the first validation code to a useful message", () => {
    expect(accountErrorMessage({ code: "validation_failed", errors: ["password_too_short"] })).toBe(
      "Le mot de passe doit contenir au moins 12 caractères.",
    );
  });

  it("keeps a generic message for unknown codes", () => {
    expect(accountErrorMessage({ code: "unknown_code", errors: [] })).toBe(
      "Une erreur est survenue. Réessaie dans un instant.",
    );
    expect(validationErrorMessage([])).toBe("Les informations saisies ne sont pas valides.");
  });

  it("explains a locked account and the rate limit", () => {
    expect(accountErrorMessage({ code: "account_locked", errors: [] })).toContain("quinze minutes");
    expect(accountErrorMessage({ code: "rate_limited", errors: [] })).toContain("une minute");
  });
});
