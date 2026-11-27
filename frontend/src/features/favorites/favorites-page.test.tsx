import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { FavoritesPage } from "./favorites-page";

const EMPTY_PROPS = {
  items: [],
  removingId: null,
  errorMessage: null,
  onRemove: () => {},
  onShowOnMap: () => {},
  onSignIn: () => {},
  onExploreMap: () => {},
} as const;

describe("favorites page", () => {
  it("asks the visitor to sign in when signed out", () => {
    const markup = renderToStaticMarkup(
      <FavoritesPage {...EMPTY_PROPS} signedIn={false} isLoading={false} />,
    );

    expect(markup).toContain("Favoris");
    expect(markup).toContain("Connecte-toi pour retrouver les favoris");
    expect(markup).toContain("Se connecter");
  });

  it("shows a loading message while the favorites are fetched", () => {
    const markup = renderToStaticMarkup(<FavoritesPage {...EMPTY_PROPS} signedIn isLoading />);

    expect(markup).toContain("Chargement des favoris");
    expect(markup).not.toContain("Se connecter");
  });

  it("shows the empty state with a map entry point", () => {
    const markup = renderToStaticMarkup(
      <FavoritesPage {...EMPTY_PROPS} signedIn={true} isLoading={false} />,
    );

    expect(markup).toContain("Aucun favori pour l&#x27;instant");
    expect(markup).toContain("Explorer la carte");
  });

  it("lists saved neighborhoods with remove and map actions", () => {
    const markup = renderToStaticMarkup(
      <FavoritesPage
        {...EMPTY_PROPS}
        signedIn
        isLoading={false}
        items={[
          {
            targetType: "neighborhood",
            targetId: "40512",
            name: "Caferağa",
            district: "Kadıköy",
            createdAt: "2026-11-06T10:00:00+00:00",
          },
          {
            targetType: "neighborhood",
            targetId: "40275",
            name: "Cihangir",
            district: null,
            createdAt: "2026-11-06T09:00:00+00:00",
          },
        ]}
      />,
    );

    expect(markup).toContain("Caferağa");
    expect(markup).toContain("Kadıköy");
    expect(markup).toContain("Cihangir");
    expect(markup.match(/Voir sur la carte/g)).toHaveLength(2);
    expect(markup.match(/Retirer/g)).toHaveLength(2);
  });

  it("marks the row being removed", () => {
    const markup = renderToStaticMarkup(
      <FavoritesPage
        {...EMPTY_PROPS}
        signedIn
        isLoading={false}
        removingId="40512"
        items={[
          {
            targetType: "neighborhood",
            targetId: "40512",
            name: "Caferağa",
            district: "Kadıköy",
            createdAt: "2026-11-06T10:00:00+00:00",
          },
        ]}
      />,
    );

    expect(markup).toContain("Retrait…");
  });

  it("shows an explicit error message", () => {
    const markup = renderToStaticMarkup(
      <FavoritesPage
        {...EMPTY_PROPS}
        signedIn
        isLoading={false}
        errorMessage="Ce quartier n'est pas encore disponible."
      />,
    );

    expect(markup).toContain('role="alert"');
    expect(markup).toContain("Ce quartier n&#x27;est pas encore disponible.");
  });
});
