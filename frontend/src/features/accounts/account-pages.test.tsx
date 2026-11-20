import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { ConfirmEmailPage } from "./confirm-email-page";
import { LoginPage } from "./login-page";
import { SignupPage } from "./signup-page";

describe("registration page", () => {
  it("renders the three required fields and the login link", () => {
    const markup = renderToStaticMarkup(
      <SignupPage
        locale="fr"
        onSubmit={async () => ({ status: "success" })}
        onGoToLogin={() => {}}
      />,
    );

    expect(markup).toContain("Créer un compte");
    expect(markup).toContain("Adresse électronique");
    expect(markup).toContain("Nom affiché");
    expect(markup).toContain("Mot de passe");
    expect(markup).toContain('type="password"');
    expect(markup).toContain('autoComplete="new-password"');
    expect(markup).toContain("12 caractères minimum");
    expect(markup).toContain("Se connecter");
  });
});

describe("login page", () => {
  it("renders the email and password fields with the registration link", () => {
    const markup = renderToStaticMarkup(
      <LoginPage onSubmit={async () => ({ status: "success" })} onGoToSignup={() => {}} />,
    );

    expect(markup).toContain("Se connecter");
    expect(markup).toContain("Adresse électronique");
    expect(markup).toContain('autoComplete="current-password"');
    expect(markup).toContain("Créer un compte");
    expect(markup).toContain("La carte, le chat et les données restent utilisables sans compte.");
  });
});

describe("email confirmation page", () => {
  it("announces the verification while the token is checked", () => {
    const markup = renderToStaticMarkup(
      <ConfirmEmailPage status={{ status: "verifying" }} onGoToLogin={() => {}} />,
    );

    expect(markup).toContain("Vérification du lien en cours");
    expect(markup).not.toContain("Aller à la connexion");
  });

  it("invites the person to log in after a successful confirmation", () => {
    const markup = renderToStaticMarkup(
      <ConfirmEmailPage status={{ status: "success" }} onGoToLogin={() => {}} />,
    );

    expect(markup).toContain("Adresse confirmée");
    expect(markup).toContain("Ton compte est actif");
    expect(markup).toContain("Aller à la connexion");
  });

  it("translates an invalid token into an actionable message", () => {
    const markup = renderToStaticMarkup(
      <ConfirmEmailPage
        status={{ status: "error", code: "invalid_token", errors: [] }}
        onGoToLogin={() => {}}
      />,
    );

    expect(markup).toContain('role="alert"');
    expect(markup).toContain("Ce lien de confirmation n&#x27;est plus valide");
  });
});
