import { describe, expect, it } from "vite-plus/test";
import { selectDefaultCity } from "./city-client";
import type { City } from "./metric-api-schema";

const istanbul: City = {
  plateCode: 34,
  name: "İstanbul",
  officialCode: "TR-34",
  center: { longitude: 28.98, latitude: 41.01 },
};

const ankara: City = {
  plateCode: 6,
  name: "Ankara",
  officialCode: "TR-06",
  center: { longitude: 32.85, latitude: 39.93 },
};

describe("default city", () => {
  it("returns Istanbul when the API publishes several provinces", () => {
    expect(selectDefaultCity([ankara, istanbul])).toEqual(istanbul);
  });

  it("falls back to the first published city when Istanbul is absent", () => {
    expect(selectDefaultCity([ankara])).toEqual(ankara);
  });

  it("returns null when the API publishes no city", () => {
    expect(selectDefaultCity([])).toBeNull();
  });
});
