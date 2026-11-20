import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { AccountMenuView } from "./account-menu";

describe("account menu", () => {
  it("offers a sign-in entry when signed out", () => {
    const markup = renderToStaticMarkup(
      <AccountMenuView
        session={null}
        isPending={false}
        isLoggingOut={false}
        onSignIn={() => {}}
        onOpenFavorites={() => {}}
        onOpenAdministration={() => {}}
        onLogout={() => {}}
      />,
    );

    expect(markup).toContain("Se connecter");
    expect(markup).not.toContain("Favoris");
  });

  it("shows the display name, the favorites entry and the logout action when signed in", () => {
    const markup = renderToStaticMarkup(
      <AccountMenuView
        session={{
          userId: "user-1",
          email: "camille@example.test",
          displayName: "Camille",
          locale: "fr",
          isDemo: false,
          isAdmin: false,
        }}
        isPending={false}
        isLoggingOut={false}
        onSignIn={() => {}}
        onOpenFavorites={() => {}}
        onOpenAdministration={() => {}}
        onLogout={() => {}}
      />,
    );

    expect(markup).toContain("Camille");
    expect(markup).toContain("camille@example.test");
    expect(markup).toContain("Favoris");
    expect(markup).toContain("Se déconnecter");
    expect(markup).not.toContain("Se connecter");
  });

  it("renders nothing while the session is loading", () => {
    const markup = renderToStaticMarkup(
      <AccountMenuView
        session={null}
        isPending
        isLoggingOut={false}
        onSignIn={() => {}}
        onOpenFavorites={() => {}}
        onOpenAdministration={() => {}}
        onLogout={() => {}}
      />,
    );

    expect(markup).toBe("");
  });
});

describe("account menu administration entry", () => {
  const session = {
    userId: "user-1",
    email: "camille@example.test",
    displayName: "Camille",
    locale: "fr",
    isDemo: false,
  };

  it("shows the administration entry to an administrator", () => {
    const markup = renderToStaticMarkup(
      <AccountMenuView
        session={{ ...session, isAdmin: true }}
        isPending={false}
        isLoggingOut={false}
        onSignIn={() => {}}
        onOpenFavorites={() => {}}
        onOpenAdministration={() => {}}
        onLogout={() => {}}
      />,
    );

    expect(markup).toContain("Administration");
  });

  it("hides the administration entry from a regular account", () => {
    const markup = renderToStaticMarkup(
      <AccountMenuView
        session={{ ...session, isAdmin: false }}
        isPending={false}
        isLoggingOut={false}
        onSignIn={() => {}}
        onOpenFavorites={() => {}}
        onOpenAdministration={() => {}}
        onLogout={() => {}}
      />,
    );

    expect(markup).not.toContain("Administration");
  });
});
