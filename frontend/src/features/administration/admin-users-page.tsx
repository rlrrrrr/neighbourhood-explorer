import { useState, type ReactNode } from "react";
import { Button } from "../../components/ui/button";
import type { AdminUser } from "../../api/account-schemas";

interface AdminUsersPageProps {
  signedIn: boolean;
  isAdmin: boolean;
  isLoading: boolean;
  users: readonly AdminUser[];
  currentUserId: string;
  removingUserId: string | null;
  errorMessage: ReactNode;
  onRemove: (userId: string) => void;
  onSignIn: () => void;
}

export function AdminUsersPage({
  signedIn,
  isAdmin,
  isLoading,
  users,
  currentUserId,
  removingUserId,
  errorMessage,
  onRemove,
  onSignIn,
}: AdminUsersPageProps) {
  const [confirmationUserId, setConfirmationUserId] = useState<string | null>(null);

  return (
    <section className="mx-auto w-full max-w-3xl flex-1 px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight">
        <fbt desc="Title of the admin accounts screen">Comptes</fbt>
      </h1>
      <p className="mt-2 text-sm text-text-secondary">
        <fbt desc="Subtitle of the admin accounts screen">
          Les comptes enregistrés dans la base partagée. Retirer un compte supprime son profil et ses
          favoris.
        </fbt>
      </p>

      {errorMessage && (
        <p role="alert" className="mt-4 text-sm font-semibold text-info">
          {errorMessage}
        </p>
      )}

      {!signedIn && !isLoading && (
        <div className="mt-8 grid justify-items-start gap-3 rounded-2xl bg-surface p-5 shadow-elevated">
          <p className="text-sm text-text-secondary">
            <fbt desc="Message shown on the admin screen when signed out">
              Connecte-toi avec un compte administrateur pour gérer les comptes.
            </fbt>
          </p>
          <Button onClick={onSignIn}>
            <fbt desc="Button opening the login page from the admin screen">Se connecter</fbt>
          </Button>
        </div>
      )}

      {signedIn && !isAdmin && !isLoading && (
        <div className="mt-8 rounded-2xl bg-surface p-5 shadow-elevated">
          <p className="text-sm text-text-secondary">
            <fbt desc="Message shown to a signed-in account without the admin role">
              Cette page est réservée aux administrateurs.
            </fbt>
          </p>
        </div>
      )}

      {isAdmin && isLoading && (
        <p aria-live="polite" className="mt-8 text-sm text-text-secondary">
          <fbt desc="Loading message on the admin accounts screen">Chargement des comptes…</fbt>
        </p>
      )}

      {isAdmin && !isLoading && users.length === 0 && (
        <div className="mt-8 rounded-2xl bg-surface p-5 shadow-elevated">
          <p className="text-sm text-text-secondary">
            <fbt desc="Empty state of the admin accounts screen">Aucun compte enregistré.</fbt>
          </p>
        </div>
      )}

      {isAdmin && !isLoading && users.length > 0 && (
        <ul className="mt-8 grid gap-3">
          {users.map((user) => {
            const isCurrentUser = user.userId === currentUserId;
            const isRemoving = removingUserId === user.userId;
            const isConfirming = confirmationUserId === user.userId;

            return (
              <li
                key={user.userId}
                className="grain flex flex-wrap items-center gap-3 rounded-2xl bg-surface p-4 shadow-elevated"
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold">{user.displayName}</p>
                  <p className="truncate text-xs text-text-secondary">{user.email}</p>
                  <p className="mt-1 flex flex-wrap gap-2 text-xs text-text-secondary">
                    <span>{user.createdAt.slice(0, 10)}</span>
                    <span>
                      {user.emailConfirmed ? (
                        <fbt desc="Badge for an account whose email address is confirmed">
                          Confirmé
                        </fbt>
                      ) : (
                        <fbt desc="Badge for an account whose email address is not confirmed yet">
                          En attente
                        </fbt>
                      )}
                    </span>
                    {user.isAdmin && (
                      <span className="font-semibold text-action">
                        <fbt desc="Badge marking an administrator account">Admin</fbt>
                      </span>
                    )}
                    {user.isDemo && (
                      <span>
                        <fbt desc="Badge marking a demonstration account">Démo</fbt>
                      </span>
                    )}
                  </p>
                </div>

                {isCurrentUser ? (
                  <span className="text-xs text-text-secondary">
                    <fbt desc="Label replacing the delete button on the administrator's own row">
                      Compte actuel
                    </fbt>
                  </span>
                ) : isConfirming ? (
                  <>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={isRemoving}
                      onClick={() => {
                        setConfirmationUserId(null);
                        onRemove(user.userId);
                      }}
                    >
                      {isRemoving ? (
                        <fbt desc="Delete account button while the request is running">Retrait…</fbt>
                      ) : (
                        <fbt desc="Button confirming the removal of an account">Confirmer</fbt>
                      )}
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={isRemoving}
                      onClick={() => setConfirmationUserId(null)}
                    >
                      <fbt desc="Button cancelling the removal of an account">Annuler</fbt>
                    </Button>
                  </>
                ) : (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setConfirmationUserId(user.userId)}
                  >
                    <fbt desc="Button starting the removal of an account">Retirer</fbt>
                  </Button>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
