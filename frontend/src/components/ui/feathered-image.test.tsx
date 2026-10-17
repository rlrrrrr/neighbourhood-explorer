import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import { FeatheredImage } from "./feathered-image";

describe("feathered image", () => {
  const markup = renderToStaticMarkup(
    <FeatheredImage
      src="/assets/photo.jpg"
      alt="Une photo d'Istanbul"
      loading="lazy"
      decoding="async"
      className="h-44 w-full"
    />,
  );

  it("shows a sheen skeleton over a blurred, hidden image until load", () => {
    expect(markup).toContain("skeleton");
    expect(markup).toContain('aria-hidden="true"');
    expect(markup).toContain("opacity-0");
    expect(markup).toContain("blur-[6px]");
    expect(markup).toContain("duration-500");
  });

  it("feathers both the skeleton and the image edges into the background", () => {
    const feathers = markup.match(/feathered-image/g) ?? [];
    expect(feathers).toHaveLength(2);
  });

  it("keeps the image semantics and the sizing class on the wrapper", () => {
    expect(markup).toContain('src="/assets/photo.jpg"');
    expect(markup).toContain('alt="Une photo d&#x27;Istanbul"');
    expect(markup).toContain('loading="lazy"');
    expect(markup).toContain('decoding="async"');
    expect(markup).toContain('class="relative block h-44 w-full"');
    expect(markup).toContain("object-cover");
  });
});
