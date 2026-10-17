import { describe, expect, it } from "vite-plus/test";
import { hasMoreToScroll, hasOverflow } from "./scroll-affordance";

describe("scroll affordance", () => {
  it("reports more content only while something is still hidden", () => {
    expect(hasMoreToScroll({ offset: 0, viewport: 300, content: 600 })).toBe(true);
    expect(hasMoreToScroll({ offset: 200, viewport: 300, content: 600 })).toBe(true);
    expect(hasMoreToScroll({ offset: 300, viewport: 300, content: 600 })).toBe(false);
    expect(hasMoreToScroll({ offset: 400, viewport: 300, content: 600 })).toBe(false);
  });

  it("reports nothing to scroll when the content fits", () => {
    expect(hasMoreToScroll({ offset: 0, viewport: 600, content: 600 })).toBe(false);
    expect(hasMoreToScroll({ offset: 0, viewport: 600, content: 400 })).toBe(false);
  });

  it("tells an overflowing container apart from a scrollable one", () => {
    expect(hasOverflow({ viewport: 390, content: 720 })).toBe(true);
    expect(hasOverflow({ viewport: 390, content: 390 })).toBe(false);
  });
});
