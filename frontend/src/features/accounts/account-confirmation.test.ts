import { describe, expect, it } from "vite-plus/test";
import { confirmationCredentialsFromSearch } from "./account-confirmation";

describe("confirmation credentials", () => {
  it("reads the user id and token from the confirmation link", () => {
    expect(confirmationCredentialsFromSearch("?userId=user-1&token=abc%2Bdef%3D")).toEqual({
      userId: "user-1",
      token: "abc+def=",
    });
  });

  it("rejects a link missing the token", () => {
    expect(confirmationCredentialsFromSearch("?userId=user-1")).toBeNull();
  });

  it("rejects a link missing the user id", () => {
    expect(confirmationCredentialsFromSearch("?token=abc")).toBeNull();
  });

  it("rejects blank and oversized credentials", () => {
    expect(confirmationCredentialsFromSearch("?userId=%20&token=abc")).toBeNull();
    expect(
      confirmationCredentialsFromSearch(`?userId=user-1&token=${"a".repeat(2049)}`),
    ).toBeNull();
  });

  it("accepts a search string without the leading question mark", () => {
    expect(confirmationCredentialsFromSearch("userId=user-1&token=abc")).toEqual({
      userId: "user-1",
      token: "abc",
    });
  });
});
