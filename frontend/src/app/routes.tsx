import { Outlet, useLocation, useNavigate, useSearch } from "@tanstack/react-router";
import { fbt } from "fbtee";
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

function useOpenView() {
  const navigate = useNavigate();
  return (view: ViewName) => {
    void navigate({ to: viewPath(view), search: (previous) => previous });
  };
}

function useOpenMapAt() {
  const navigate = useNavigate();
  return (neighborhood: string) => {
    void navigate({
      to: viewPath("map"),
      search: (previous) => searchWithNeighborhood(previous, neighborhood),
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
    <AppShell view={viewForPath(location.pathname)} onNavigate={openView}>
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
      selectedNeighborhood={search.neighborhood ?? null}
      onSelectNeighborhood={(neighborhood) => {
        void navigate({
          to: viewPath("map"),
          search: (previous) => searchWithNeighborhood(previous, neighborhood),
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
