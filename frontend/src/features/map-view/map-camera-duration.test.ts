import { afterEach, describe, expect, it, vi } from "vite-plus/test";
import { mapCameraAnimationDuration } from "./map-camera-duration";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("map camera animation duration", () => {
  it("animates for 700 ms by default", () => {
    vi.stubGlobal("window", { matchMedia: () => ({ matches: false }) });

    expect(mapCameraAnimationDuration()).toBe(700);
  });

  it("jumps immediately when reduced motion is requested", () => {
    vi.stubGlobal("window", { matchMedia: () => ({ matches: true }) });

    expect(mapCameraAnimationDuration()).toBe(0);
  });
});
