import { useState } from "react";
import { Button } from "../../components/ui/button";
import { AccountField } from "./account-field";
import { accountErrorMessage } from "./account-error-message";
import type { AccountActionResult } from "./account-action-result";

interface LoginPageProps {
  onSubmit: (input: { email: string; password: string }) => Promise<AccountActionResult>;
  onGoToSignup: () => void;
}

export function LoginPage({ onSubmit, onGoToSignup }: LoginPageProps) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [result, setResult] = useState<AccountActionResult | null>(null);

  return (
    <section className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight text-balance">
        <fbt desc="Title of the login page">Se connecter</fbt>
      </h1>
      <p className="mt-2 text-sm text-text-secondary">
        <fbt desc="Subtitle of the login page">
          Retrouve tes favoris. La carte, le chat et les données restent utilisables sans compte.
        </fbt>
      </p>

      <form
        className="mt-8 grid gap-4"
        onSubmit={async (event) => {
          event.preventDefault();
          setIsSubmitting(true);
          setResult(null);
          const nextResult = await onSubmit({ email, password });
          setResult(nextResult);
          setIsSubmitting(false);
        }}
      >
        <AccountField
          id="login-email"
          type="email"
          autoComplete="email"
          label={<fbt desc="Label of the email field on the login form">Adresse électronique</fbt>}
          value={email}
          onChange={setEmail}
        />
        <AccountField
          id="login-password"
          type="password"
          autoComplete="current-password"
          label={<fbt desc="Label of the password field on the login form">Mot de passe</fbt>}
          value={password}
          onChange={setPassword}
        />

        {result?.status === "error" && (
          <p role="alert" className="text-sm font-semibold text-info">
            {accountErrorMessage({ code: result.code, errors: result.errors })}
          </p>
        )}

        <Button type="submit" disabled={isSubmitting} className="mt-2">
          {isSubmitting ? (
            <fbt desc="Button label while the login request is running">Connexion…</fbt>
          ) : (
            <fbt desc="Submit button of the login form">Se connecter</fbt>
          )}
        </Button>
      </form>

      <p className="mt-6 text-sm text-text-secondary">
        <fbt desc="Sentence introducing the registration link on the login page">
          Pas encore de compte ?
        </fbt>{" "}
        <button
          type="button"
          onClick={onGoToSignup}
          className="cursor-pointer font-semibold text-text underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
        >
          <fbt desc="Link from the login page to the registration page">Créer un compte</fbt>
        </button>
      </p>
    </section>
  );
}
