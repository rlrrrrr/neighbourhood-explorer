import { useNavigate } from "@tanstack/react-router";
import { fbt } from "fbtee";
import { Button } from "../../components/ui/button";
import { HeaderMenu } from "../../app/header-menu";
import type { AccountSession } from "../../api/account-schemas";
import { useLogoutMutation, useSession } from "./account-queries";

interface AccountMenuViewProps {
  session: AccountSession | null;
  isPending: boolean;
  isLoggingOut: boolean;
  onSignIn: () => void;
  onOpenFavorites: () => void;
  onOpenAdministration: () => void;
  onLogout: () => void;
}

export function AccountMenuView({
  session,
  isPending,
  isLoggingOut,
  onSignIn,
  onOpenFavorites,
  onOpenAdministration,
  onLogout,
}: AccountMenuViewProps) {
  if (isPending) {
    return null;
  }

  if (session === null) {
    return (
      <Button variant="ghost" size="sm" onClick={onSignIn}>
        <fbt desc="Header button opening the login page">Se connecter</fbt>
      </Button>
    );
  }

  return (
    <HeaderMenu
      id="account-menu"
      menuLabel={fbt("Compte", "Accessible label of the account menu")}
      trigger={<span className="max-w-32 truncate">{session.displayName}</span>}
      panelClassName="min-w-44"
    >
      {(close) => (
        <>
          <p className="px-3 py-2 text-xs text-text-secondary">{session.email}</p>
          <Button
            variant="ghost"
            size="sm"
            className="justify-start"
            onClick={() => {
              close();
              onOpenFavorites();
            }}
          >
            <fbt desc="Account menu entry opening the favorites screen">Favoris</fbt>
          </Button>
          {session.isAdmin && (
            <Button
              variant="ghost"
              size="sm"
              className="justify-start"
              onClick={() => {
                close();
                onOpenAdministration();
              }}
            >
              <fbt desc="Account menu entry opening the account administration screen">
                Administration
              </fbt>
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            className="justify-start"
            disabled={isLoggingOut}
            onClick={() => {
              close();
              onLogout();
            }}
          >
            {isLoggingOut ? (
              <fbt desc="Account menu entry while the logout request is running">Déconnexion…</fbt>
            ) : (
              <fbt desc="Account menu entry logging the person out">Se déconnecter</fbt>
            )}
          </Button>
        </>
      )}
    </HeaderMenu>
  );
}

export function AccountMenu() {
  const navigate = useNavigate();
  const sessionQuery = useSession();
  const logoutMutation = useLogoutMutation();

  return (
    <AccountMenuView
      session={sessionQuery.isPending ? null : (sessionQuery.data ?? null)}
      isPending={sessionQuery.isPending}
      isLoggingOut={logoutMutation.isPending}
      onSignIn={() => {
        void navigate({ to: "/login" });
      }}
      onOpenFavorites={() => {
        void navigate({ to: "/favorites" });
      }}
      onOpenAdministration={() => {
        void navigate({ to: "/admin/users" });
      }}
      onLogout={() => {
        logoutMutation.mutate(undefined, {
          onSuccess: () => {
            void navigate({ to: "/" });
          },
        });
      }}
    />
  );
}
