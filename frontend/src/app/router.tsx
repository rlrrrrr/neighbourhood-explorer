import { createRootRoute, createRoute, createRouter } from "@tanstack/react-router";
import { AppLayout, ChatRoute, DataRoute, HomeRoute, MapRoute, UnknownPageView } from "./routes";
import { parseSearchString, stringifySearchObject, validateSearch } from "./url-state";

const rootRoute = createRootRoute({
  validateSearch,
  component: AppLayout,
  notFoundComponent: UnknownPageView,
});

const homeRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: HomeRoute,
});
const chatRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/chat",
  component: ChatRoute,
});
const mapRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/map",
  component: MapRoute,
});
const dataRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/data",
  component: DataRoute,
});

const routeTree = rootRoute.addChildren([homeRoute, chatRoute, mapRoute, dataRoute]);

export const router = createRouter({
  routeTree,
  parseSearch: parseSearchString,
  stringifySearch: stringifySearchObject,
});

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
