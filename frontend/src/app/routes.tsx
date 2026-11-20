import { useEffect, useRef, useState } from "react";
import { Outlet, useLocation, useNavigate, useSearch } from "@tanstack/react-router";
import { fbt, useLocaleContext } from "fbtee";
import { AppShell } from "./app-shell";
import {
  criteriaFromSearch,
  searchWithCriteria,
  searchWithNeighborhood,
  searchWithTableSort,
  tableSortFromSearch,
  viewForPath,
  viewPath,
  type ViewName,
  type ViewPath,
} from "./url-state";
import { HomeView } from "../features/home-view/home-view";
import { ChatView } from "../features/chat-view/chat-view";
import { MapView } from "../features/map-view/map-view";
import { DataView } from "../features/data-view/data-view";
import { toggleCriterion, withoutCriterion, type Criteria } from "../features/criteria/criteria";
import { AccountMenu } from "../features/accounts/account-menu";
import {
  accountActionResultFromError,
  type AccountActionResult,
} from "../features/accounts/account-action-result";
import { accountErrorMessage } from "../features/accounts/account-error-message";
import { confirmationCredentialsFromSearch } from "../features/accounts/account-confirmation";
import { toAccountLocale } from "../features/accounts/account-locale";
import {
  useAdminUsers,
  useConfirmEmailMutation,
  useFavorites,
  useLoginMutation,
  useRegisterMutation,
  useRemoveAdminUserMutation,
  useRemoveFavoriteMutation,
  useSession,
} from "../features/accounts/account-queries";
import { ConfirmEmailPage, type ConfirmEmailStatus } from "../features/accounts/confirm-email-page";
import { LoginPage } from "../features/accounts/login-page";
import { SignupPage } from "../features/accounts/signup-page";
import { FavoritesPage } from "../features/favorites/favorites-page";
import { AdminUsersPage } from "../features/administration/admin-users-page";

function useOpenView() {
  const navigate = useNavigate();
  return (view: ViewName) => {
    void navigate({ to: viewPath(view), search: (previous) => previous });
  };
}

function useOpenMapAt() {
  const navigate = useNavigate();
  return (neighborhoodId: number) => {
    void navigate({
      to: viewPath("map"),
      search: (previous) => searchWithNeighborhood(previous, neighborhoodId),
    });
  };
}

function useCriterionWriter(to: ViewPath) {
  const navigate = useNavigate();
  return (metricKey: string, change: (criteria: Criteria, metricKey: string) => Criteria) => {
    void navigate({
      to,
      search: (previous) =>
        searchWithCriteria(previous, change(criteriaFromSearch(previous), metricKey)),
    });
  };
}

export function AppLayout() {
  const location = useLocation();
  const openView = useOpenView();

  return (
    <AppShell
      view={viewForPath(location.pathname)}
      onNavigate={openView}
      accountSlot={<AccountMenu />}
    >
      <Outlet />
    </AppShell>
  );
}

export function HomeRoute() {
  const openView = useOpenView();

  return <HomeView onNavigate={openView} />;
}

export function ChatRoute() {
  const search = useSearch({ from: "/chat" });
  const openMapAt = useOpenMapAt();
  const changeCriterion = useCriterionWriter(viewPath("chat"));

  return (
    <ChatView
      criteria={criteriaFromSearch(search)}
      onToggleCriterion={(metricKey) => changeCriterion(metricKey, toggleCriterion)}
      onShowOnMap={openMapAt}
    />
  );
}

export function MapRoute() {
  const navigate = useNavigate();
  const search = useSearch({ from: "/map" });
  const changeCriterion = useCriterionWriter(viewPath("map"));

  return (
    <MapView
      criteria={criteriaFromSearch(search)}
      onRemoveCriterion={(metricKey) => changeCriterion(metricKey, withoutCriterion)}
      selectedNeighborhoodId={search.neighborhood ?? null}
      onSelectNeighborhood={(neighborhoodId) => {
        void navigate({
          to: viewPath("map"),
          search: (previous) => searchWithNeighborhood(previous, neighborhoodId),
        });
      }}
    />
  );
}

export function DataRoute() {
  const navigate = useNavigate();
  const search = useSearch({ from: "/data" });
  const openMapAt = useOpenMapAt();
  const changeCriterion = useCriterionWriter(viewPath("data"));

  return (
    <DataView
      criteria={criteriaFromSearch(search)}
      onRemoveCriterion={(metricKey) => changeCriterion(metricKey, withoutCriterion)}
      sort={tableSortFromSearch(search)}
      onSortChange={(sort) => {
        void navigate({
          to: viewPath("data"),
          search: (previous) => searchWithTableSort(previous, sort),
        });
      }}
      onShowOnMap={openMapAt}
    />
  );
}

export function LoginRoute() {
  const navigate = useNavigate();
  const loginMutation = useLoginMutation();

  return (
    <LoginPage
      onSubmit={async (input) => {
        try {
          await loginMutation.mutateAsync(input);
          await navigate({ to: "/" });
          return { status: "success" };
        } catch (error) {
          return accountActionResultFromError(error);
        }
      }}
      onGoToSignup={() => void navigate({ to: "/signup" })}
    />
  );
}

export function SignupRoute() {
  const navigate = useNavigate();
  const { locale } = useLocaleContext();
  const registerMutation = useRegisterMutation();

  return (
    <SignupPage
      locale={toAccountLocale(locale)}
      onSubmit={async (input): Promise<AccountActionResult> => {
        try {
          await registerMutation.mutateAsync(input);
          return { status: "success" };
        } catch (error) {
          return accountActionResultFromError(error);
        }
      }}
      onGoToLogin={() => void navigate({ to: "/login" })}
    />
  );
}

export function ConfirmEmailRoute() {
  const navigate = useNavigate();
  const { mutate, isError, isSuccess, error } = useConfirmEmailMutation();
  const [credentials] = useState(() =>
    confirmationCredentialsFromSearch(typeof window === "undefined" ? "" : window.location.search),
  );
  const hasRequestedConfirmation = useRef(false);

  useEffect(() => {
    if (credentials === null || hasRequestedConfirmation.current) {
      return;
    }
    hasRequestedConfirmation.current = true;
    mutate(credentials);
  }, [credentials, mutate]);

  let status: ConfirmEmailStatus = { status: "verifying" };
  if (credentials === null) {
    status = { status: "error", code: "invalid_token", errors: [] };
  } else if (isSuccess) {
    status = { status: "success" };
  } else if (isError) {
    const failure = accountActionResultFromError(error);
    status = { status: "error", code: failure.code, errors: failure.errors };
  }

  return <ConfirmEmailPage status={status} onGoToLogin={() => void navigate({ to: "/login" })} />;
}

export function FavoritesRoute() {
  const navigate = useNavigate();
  const sessionQuery = useSession();
  const session = sessionQuery.data ?? null;
  const favoritesQuery = useFavorites(session);
  const removeFavoriteMutation = useRemoveFavoriteMutation();

  const failure = favoritesQuery.error ?? removeFavoriteMutation.error;
  const errorMessage = failure ? accountErrorMessage(accountActionResultFromError(failure)) : null;

  return (
    <FavoritesPage
      signedIn={session !== null}
      isLoading={sessionQuery.isPending || (session !== null && favoritesQuery.isPending)}
      items={favoritesQuery.data ?? []}
      removingId={
        removeFavoriteMutation.isPending ? (removeFavoriteMutation.variables ?? null) : null
      }
      errorMessage={errorMessage}
      onRemove={(targetId) => removeFavoriteMutation.mutate(targetId)}
      onShowOnMap={(targetId) => {
        const neighborhoodId = Number(targetId);
        void navigate({
          to: "/map",
          search: (previous) =>
            Number.isSafeInteger(neighborhoodId) && neighborhoodId > 0
              ? searchWithNeighborhood(previous, neighborhoodId)
              : previous,
        });
      }}
      onSignIn={() => void navigate({ to: "/login" })}
      onExploreMap={() => void navigate({ to: "/map" })}
    />
  );
}

export function AdminUsersRoute() {
  const navigate = useNavigate();
  const sessionQuery = useSession();
  const session = sessionQuery.data ?? null;
  const usersQuery = useAdminUsers(session);
  const removeUserMutation = useRemoveAdminUserMutation();

  const failure = usersQuery.error ?? removeUserMutation.error;
  const errorMessage = failure ? accountErrorMessage(accountActionResultFromError(failure)) : null;

  return (
    <AdminUsersPage
      signedIn={session !== null}
      isAdmin={session?.isAdmin === true}
      isLoading={sessionQuery.isPending || (session?.isAdmin === true && usersQuery.isPending)}
      users={usersQuery.data ?? []}
      currentUserId={session?.userId ?? ""}
      removingUserId={removeUserMutation.isPending ? (removeUserMutation.variables ?? null) : null}
      errorMessage={errorMessage}
      onRemove={(userId) => removeUserMutation.mutate(userId)}
      onSignIn={() => void navigate({ to: "/login" })}
    />
  );
}

export function UnknownPageView() {
  const unknownPageTitle = fbt(
    "Cette page n'existe pas",
    "Title of the page shown when the URL matches no view",
  );
  const unknownPageHint = fbt(
    "Choisis une vue dans la barre de navigation.",
    "Hint shown under the title of the unknown page",
  );

  return (
    <div className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center gap-2 px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight text-balance">{unknownPageTitle}</h1>
      <p className="text-sm text-text-secondary">{unknownPageHint}</p>
    </div>
  );
}
