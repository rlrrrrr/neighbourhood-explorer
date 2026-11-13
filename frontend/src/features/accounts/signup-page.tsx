import { useState } from "react";
import { Button } from "../../components/ui/button";
import { AccountField } from "./account-field";
import { accountErrorMessage } from "./account-error-message";
import type { AccountActionResult } from "./account-action-result";

interface SignupPageProps {
  locale: "fr" | "en" | "tr" | "ar";
  onSubmit: (input: {
    email: string;
    displayName: string;
    password: string;
    locale: "fr" | "en" | "tr" | "ar";
  }) => Promise<AccountActionResult>;
  onGoToLogin: () => void;
}

export function SignupPage({ locale, onSubmit, onGoToLogin }: SignupPageProps) {
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [result, setResult] = useState<AccountActionResult | null>(null);

  if (result?.status === "success") {
    return (
      <section className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center px-4 py-12 sm:px-8">
        <div className="grain rounded-2xl bg-surface p-6 shadow-elevated">
          <h1 className="text-2xl font-bold tracking-tight text-balance">
            <fbt desc="Title shown after a successful registration">Vérifie ta boîte</fbt>
          </h1>
          <p className="mt-3 text-sm text-text-secondary">
            <fbt desc="Message shown after registration, explaining the confirmation email">
              Nous t'avons envoyé un lien de confirmation. Ouvre-le pour activer ton compte, puis
              reviens te connecter.
            </fbt>
          </p>
          <Button className="mt-6" onClick={onGoToLogin}>
            <fbt desc="Button going to the login page after registration">Aller à la connexion</fbt>
          </Button>
        </div>
      </section>
    );
  }

  return (
    <section className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight text-balance">
        <fbt desc="Title of the registration page">Créer un compte</fbt>
      </h1>
      <p className="mt-2 text-sm text-text-secondary">
        <fbt desc="Subtitle of the registration page">
          Un compte sync tes favoris entre tes appareils. La carte reste accessible sans compte.
        </fbt>
      </p>

      <form
        className="mt-8 grid gap-4"
        onSubmit={async (event) => {
          event.preventDefault();
          setIsSubmitting(true);
          setResult(null);
          const nextResult = await onSubmit({ email, displayName, password, locale });
          setResult(nextResult);
          setIsSubmitting(false);
        }}
      >
        <AccountField
          id="signup-email"
          type="email"
          autoComplete="email"
          label={
            <fbt desc="Label of the email field on the registration form">Adresse électronique</fbt>
          }
          value={email}
          onChange={setEmail}
        />
        <AccountField
          id="signup-display-name"
          autoComplete="nickname"
          label={
            <fbt desc="Label of the display name field on the registration form">Nom affiché</fbt>
          }
          value={displayName}
          onChange={setDisplayName}
          hint={
            <fbt desc="Hint under the display name field on the registration form">
              Entre 1 et 80 caractères, visible dans l'interface.
            </fbt>
          }
        />
        <AccountField
          id="signup-password"
          type="password"
          autoComplete="new-password"
          label={
            <fbt desc="Label of the password field on the registration form">Mot de passe</fbt>
          }
          value={password}
          onChange={setPassword}
          hint={
            <fbt desc="Hint under the password field on the registration form">
              12 caractères minimum. Aucune composition imposée.
            </fbt>
          }
        />

        {result?.status === "error" && (
          <p role="alert" className="text-sm font-semibold text-info">
            {accountErrorMessage({ code: result.code, errors: result.errors })}
          </p>
        )}

        <Button type="submit" disabled={isSubmitting} className="mt-2">
          {isSubmitting ? (
            <fbt desc="Button label while the registration request is running">Création…</fbt>
          ) : (
            <fbt desc="Submit button of the registration form">Créer mon compte</fbt>
          )}
        </Button>
      </form>

      <p className="mt-6 text-sm text-text-secondary">
        <fbt desc="Sentence introducing the login link on the registration page">
          Déjà un compte ?
        </fbt>{" "}
        <button
          type="button"
          onClick={onGoToLogin}
          className="cursor-pointer font-semibold text-text underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
        >
          <fbt desc="Link from the registration page to the login page">Se connecter</fbt>
        </button>
      </p>
    </section>
  );
}
