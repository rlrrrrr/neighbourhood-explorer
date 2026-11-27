import { ApiError } from "../../api/api-client";

export type AccountActionResult =
  | { status: "success" }
  | { status: "error"; code: string; errors: readonly string[] };

export type AccountActionError = Extract<AccountActionResult, { status: "error" }>;

export function accountActionResultFromError(error: unknown): AccountActionError {
  if (error instanceof ApiError) {
    return { status: "error", code: error.code, errors: error.errors };
  }
  return { status: "error", code: "unexpected_error", errors: [] };
}
