import { describe, expect, it } from "vite-plus/test";
import {
  columnScale,
  isSpringSettled,
  peakFromPointer,
  PEAK_SPRING,
  RAISED_SPRING,
  stepSpring,
  type SpringState,
} from "./dia-gradient-motion";

describe("dia gradient motion", () => {
  it("builds a stepped pyramid around the peak column", () => {
    const span = 5;
    const raised = 1;
    const scales = Array.from({ length: 11 }, (_, index) =>
      columnScale({ index, peak: 5, raised, span }),
    );

    expect(scales[5]).toBeCloseTo(1 + 260 / 172, 5);
    expect(scales[0]).toBe(1);
    expect(scales[10]).toBe(1);
    for (let index = 1; index < 5; index += 1) {
      expect(scales[index]).toBeGreaterThan(scales[index - 1]);
      expect(scales[10 - index]).toBeGreaterThan(scales[11 - index]);
    }
  });

  it("keeps every column flat while the gradient is not raised", () => {
    const scales = Array.from({ length: 11 }, (_, index) =>
      columnScale({ index, peak: 5, raised: 0, span: 5 }),
    );

    expect(new Set(scales)).toEqual(new Set([1]));
  });

  it("scales the single column up when there is nothing to spread across", () => {
    expect(columnScale({ index: 0, peak: 0, raised: 1, span: 0 })).toBeCloseTo(1 + 260 / 172, 5);
  });

  it("converges both springs to their target", () => {
    let peak: SpringState = { position: 0, velocity: 0 };
    let raised: SpringState = { position: 0, velocity: 0 };
    for (let frame = 0; frame < 60 * 4; frame += 1) {
      peak = stepSpring({ state: peak, target: 5, spring: PEAK_SPRING, deltaSeconds: 1 / 60 });
      raised = stepSpring({
        state: raised,
        target: 1,
        spring: RAISED_SPRING,
        deltaSeconds: 1 / 60,
      });
    }

    expect(peak.position).toBeCloseTo(5, 2);
    expect(raised.position).toBeCloseTo(1, 2);
    expect(isSpringSettled({ state: peak, target: 5 })).toBe(true);
    expect(isSpringSettled({ state: raised, target: 1 })).toBe(true);
  });

  it("maps the pointer across the row and clamps it to the outer columns", () => {
    expect(peakFromPointer({ pointerX: 600, rowLeft: 0, rowWidth: 1200, columnCount: 11 })).toBe(5);
    expect(peakFromPointer({ pointerX: -400, rowLeft: 0, rowWidth: 1200, columnCount: 11 })).toBe(
      0,
    );
    expect(peakFromPointer({ pointerX: 2000, rowLeft: 0, rowWidth: 1200, columnCount: 11 })).toBe(
      10,
    );
  });
});
