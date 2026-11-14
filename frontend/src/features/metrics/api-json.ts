import { isProblemDetails, type ProblemDetails } from "./metric-api-schema";

const API_BASE_URL = import.meta.env.VITE_API_URL ?? "";

export class ApiRequestError extends Error {
  readonly code: string;

  constructor({ code, message }: { code: string; message: string }) {
    super(message);
    this.name = "ApiRequestError";
    this.code = code;
  }
}

export async function getApiJson(path: string, signal: AbortSignal): Promise<unknown> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    signal,
    headers: { Accept: "application/json, application/geo+json" },
  });

  if (!response.ok) {
    const problem = await readProblemDetails(response);
    throw new ApiRequestError({
      code: problem?.code ?? `http_${response.status}`,
      message: problem?.detail ?? `L'API a répondu ${response.status}.`,
    });
  }

  return response.json();
}

async function readProblemDetails(response: Response): Promise<ProblemDetails | null> {
  try {
    const payload: unknown = await response.json();
    return isProblemDetails(payload) ? payload : null;
  } catch {
    return null;
  }
}
