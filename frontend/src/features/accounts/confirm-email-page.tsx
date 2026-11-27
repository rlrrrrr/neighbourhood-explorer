import { Button } from "../../components/ui/button";
import { accountErrorMessage } from "./account-error-message";

export type ConfirmEmailStatus =
  | { status: "verifying" }
  | { status: "success" }
  | { status: "error"; code: string; errors: readonly string[] };

interface ConfirmEmailPageProps {
  status: ConfirmEmailStatus;
  onGoToLogin: () => void;
}

export function ConfirmEmailPage({ status, onGoToLogin }: ConfirmEmailPageProps) {
  return (
    <section className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center px-4 py-12 sm:px-8">
      <div className="grain rounded-2xl bg-surface p-6 shadow-elevated">
        <h1 className="text-2xl font-bold tracking-tight text-balance">
          {status.status === "success" ? (
            <fbt desc="Title shown when the email confirmation succeeded">Adresse confirmée</fbt>
          ) : (
            <fbt desc="Title of the email confirmation page">Confirmation de l'adresse</fbt>
          )}
        </h1>

        {status.status === "verifying" && (
          <p aria-live="polite" className="mt-3 text-sm text-text-secondary">
            <fbt desc="Message shown while the email confirmation request is running">
              Vérification du lien en cours…
            </fbt>
          </p>
        )}

        {status.status === "success" && (
          <p className="mt-3 text-sm text-text-secondary">
            <fbt desc="Message shown when the email confirmation succeeded">
              Ton compte est actif. Tu peux maintenant te connecter et retrouver tes favoris.
            </fbt>
          </p>
        )}

        {status.status === "error" && (
          <p role="alert" className="mt-3 text-sm font-semibold text-info">
            {accountErrorMessage({ code: status.code, errors: status.errors })}
          </p>
        )}

        {status.status !== "verifying" && (
          <Button className="mt-6" onClick={onGoToLogin}>
            <fbt desc="Button going to the login page from the email confirmation page">
              Aller à la connexion
            </fbt>
          </Button>
        )}
      </div>
    </section>
  );
}
