import {
  apiErrorSchema,
  confirmEmailInputSchema,
  csrfResponseSchema,
  favoritesResponseSchema,
  loginInputSchema,
  meResponseSchema,
  registerInputSchema,
  registerResponseSchema,
  type AccountSession,
  type ConfirmEmailInput,
  type FavoriteItem,
  type LoginInput,
  type RegisterInput,
} from "./account-schemas";

const DEFAULT_API_BASE_URL = "http://localhost:5080";

export class ApiError extends Error {
  readonly code: string;
  readonly errors: readonly string[];
  readonly status: number;

  constructor({
    code,
    errors,
    status,
  }: {
    code: string;
    errors: readonly string[];
    status: number;
  }) {
    super(code);
    this.name = "ApiError";
    this.code = code;
    this.errors = errors;
    this.status = status;
  }
}

function apiBaseUrl(): string {
  const configuredUrl = import.meta.env.VITE_API_BASE_URL;
  return (configuredUrl || DEFAULT_API_BASE_URL).replace(/\/$/, "");
}

let csrfTokenRequest: Promise<string> | null = null;

export function invalidateCsrfToken(): void {
  csrfTokenRequest = null;
}

async function requestCsrfToken(): Promise<string> {
  const response = await fetch(`${apiBaseUrl()}/api/v1/auth/csrf`, {
    credentials: "include",
    headers: { Accept: "application/json" },
  });

  if (!response.ok) {
    throw new ApiError({ code: "csrf_failed", errors: [], status: response.status });
  }

  const payload: unknown = await response.json();
  return csrfResponseSchema.parse(payload).token;
}

export function getCsrfToken(): Promise<string> {
  if (csrfTokenRequest === null) {
    csrfTokenRequest = requestCsrfToken().catch((error: unknown) => {
      csrfTokenRequest = null;
      throw error;
    });
  }
  return csrfTokenRequest;
}

async function readPayload(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return null;
  }
}

async function apiRequest({
  path,
  method = "GET",
  body,
  csrf = false,
}: {
  path: string;
  method?: "GET" | "POST" | "PUT" | "DELETE";
  body?: unknown;
  csrf?: boolean;
}): Promise<unknown> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
  }
  if (csrf) {
    headers["X-CSRF-TOKEN"] = await getCsrfToken();
  }

  const response = await fetch(`${apiBaseUrl()}${path}`, {
    method,
    credentials: "include",
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (response.status === 204) {
    return null;
  }

  const payload = await readPayload(response);

  if (!response.ok) {
    const problem = apiErrorSchema.safeParse(payload);
    if (problem.success) {
      throw new ApiError({
        code: problem.data.code,
        errors: problem.data.errors,
        status: response.status,
      });
    }
    throw new ApiError({ code: "unexpected_error", errors: [], status: response.status });
  }

  return payload;
}

export async function getSession(): Promise<AccountSession | null> {
  try {
    const payload = await apiRequest({ path: "/api/v1/me" });
    return meResponseSchema.parse(payload);
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) {
      return null;
    }
    throw error;
  }
}

export async function registerAccount(input: RegisterInput): Promise<void> {
  const body = registerInputSchema.parse(input);
  const payload = await apiRequest({
    path: "/api/v1/auth/register",
    method: "POST",
    body,
    csrf: true,
  });
  registerResponseSchema.parse(payload);
}

export async function confirmEmailAddress(input: ConfirmEmailInput): Promise<void> {
  const body = confirmEmailInputSchema.parse(input);
  await apiRequest({
    path: "/api/v1/auth/confirm-email",
    method: "POST",
    body,
    csrf: true,
  });
}

export async function login(input: LoginInput): Promise<AccountSession> {
  const body = loginInputSchema.parse(input);
  const payload = await apiRequest({
    path: "/api/v1/auth/login",
    method: "POST",
    body,
    csrf: true,
  });
  const session = meResponseSchema.parse(payload);
  invalidateCsrfToken();
  return session;
}

export async function logout(): Promise<void> {
  await apiRequest({ path: "/api/v1/auth/logout", method: "POST", csrf: true });
  invalidateCsrfToken();
}

export async function listFavorites(): Promise<readonly FavoriteItem[]> {
  const payload = await apiRequest({ path: "/api/v1/me/favorites" });
  return favoritesResponseSchema.parse(payload).items;
}

export async function putFavorite(targetId: string): Promise<void> {
  await apiRequest({
    path: `/api/v1/me/favorites/neighborhood/${encodeURIComponent(targetId)}`,
    method: "PUT",
    csrf: true,
  });
}

export async function deleteFavorite(targetId: string): Promise<void> {
  await apiRequest({
    path: `/api/v1/me/favorites/neighborhood/${encodeURIComponent(targetId)}`,
    method: "DELETE",
    csrf: true,
  });
}
