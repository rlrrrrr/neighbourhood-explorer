import { getApiJson } from "./api-json";
import { citiesResponseSchema, type City } from "./metric-api-schema";

export const DEFAULT_CITY_PLATE_CODE = 34;

export async function fetchCities({ signal }: { signal: AbortSignal }): Promise<City[]> {
  const payload = await getApiJson({
    path: "/api/v1/cities",
    schema: citiesResponseSchema,
    signal,
  });

  return payload.cities;
}

export function selectDefaultCity(cities: readonly City[]): City | null {
  return cities.find((city) => city.plateCode === DEFAULT_CITY_PLATE_CODE) ?? cities[0] ?? null;
}
