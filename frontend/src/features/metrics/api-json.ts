import * as z from "zod";
import { problemDetailsSchema, type ProblemDetails } from "./metric-api-schema";

const API_BASE_URL = import.meta.env.VITE_API_URL ?? "";

export class ApiRequestError extends Error {
  readonly code: string;

  constructor({ code, message }: { code: string; message: string }) {
    super(message);
    this.name = "ApiRequestError";
    this.code = code;
  }
}

export async function getApiJson<Output>({
  path,
  schema,
  signal,
}: {
  path: string;
  schema: z.ZodType<Output>;
  signal: AbortSignal;
}): Promise<Output> {
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

  const payload: unknown = await response.json();
  const parsed = schema.safeParse(payload);

  if (!parsed.success) {
    throw new ApiRequestError({
      code: "invalid_response",
      message: `La réponse de ${path} est invalide.`,
    });
  }

  return parsed.data;
}

async function readProblemDetails(response: Response): Promise<ProblemDetails | null> {
  try {
    const payload: unknown = await response.json();
    const parsed = problemDetailsSchema.safeParse(payload);

    return parsed.success ? parsed.data : null;
  } catch {
    return null;
  }
}
