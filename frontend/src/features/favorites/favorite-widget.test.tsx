import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { FavoriteWidgetView } from "./favorite-widget";

function renderWidget(signedIn: boolean) {
  return renderToStaticMarkup(
    <FavoriteWidgetView signedIn={signedIn} onOpenFavorites={() => {}} />,
  );
}

describe("favorite widget", () => {
  it("renders nothing when the person is signed out", () => {
    expect(renderWidget(false)).toBe("");
  });

  it("shows the star button with an accessible label when signed in", () => {
    const markup = renderWidget(true);

    expect(markup).toContain('aria-label="Favoris"');
    expect(markup).toContain('aria-hidden="true"');
    expect(markup).toContain("lucide-star");
  });
});
