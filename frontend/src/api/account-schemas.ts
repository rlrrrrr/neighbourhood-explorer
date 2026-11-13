import { z } from "zod";
import type { components } from "../generated/api";

type ApiSchemas = components["schemas"];

export const csrfResponseSchema = z.object({
  token: z.string(),
}) satisfies z.ZodType<ApiSchemas["CsrfResponse"]>;

export const registerResponseSchema = z.object({
  status: z.literal("confirmation_sent"),
}) satisfies z.ZodType<ApiSchemas["RegisterResponse"]>;

export const meResponseSchema = z.object({
  userId: z.string(),
  email: z.string(),
  displayName: z.string(),
  locale: z.string(),
  isDemo: z.boolean(),
}) satisfies z.ZodType<ApiSchemas["MeResponse"]>;

export const favoriteSchema = z.object({
  targetType: z.literal("neighborhood"),
  targetId: z.string(),
  name: z.string().nullable().optional(),
  district: z.string().nullable().optional(),
  createdAt: z.iso.datetime(),
});

export const favoritesResponseSchema = z.object({
  items: z.array(favoriteSchema),
}) satisfies z.ZodType<ApiSchemas["FavoritesResponse"]>;

export const apiErrorSchema = z.object({
  code: z.string(),
  errors: z.array(z.string()),
}) satisfies z.ZodType<ApiSchemas["ApiError"]>;

export const registerInputSchema = z.object({
  email: z.string(),
  displayName: z.string(),
  password: z.string(),
  locale: z.enum(["fr", "en", "tr", "ar"]).nullable(),
}) satisfies z.ZodType<ApiSchemas["RegisterRequest"]>;

export const confirmEmailInputSchema = z.object({
  userId: z.string(),
  token: z.string(),
}) satisfies z.ZodType<ApiSchemas["ConfirmEmailRequest"]>;

export const loginInputSchema = z.object({
  email: z.string(),
  password: z.string(),
}) satisfies z.ZodType<ApiSchemas["LoginRequest"]>;

export type AccountSession = z.infer<typeof meResponseSchema>;
export type FavoriteItem = z.infer<typeof favoriteSchema>;
export type RegisterInput = z.infer<typeof registerInputSchema>;
export type LoginInput = z.infer<typeof loginInputSchema>;
export type ConfirmEmailInput = z.infer<typeof confirmEmailInputSchema>;
