import type { ConfirmEmailInput } from "../../api/account-schemas";

const MAX_CREDENTIAL_LENGTH = 2048;

function readCredential(value: string | null): string | null {
  const trimmed = value?.trim() ?? "";
  return trimmed.length > 0 && trimmed.length <= MAX_CREDENTIAL_LENGTH ? trimmed : null;
}

export function confirmationCredentialsFromSearch(search: string): ConfirmEmailInput | null {
  const parameters = new URLSearchParams(search);
  const userId = readCredential(parameters.get("userId"));
  const token = readCredential(parameters.get("token"));
  return userId && token ? { userId, token } : null;
}
