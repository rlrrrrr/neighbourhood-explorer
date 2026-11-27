import { createRootRoute, createRoute, createRouter } from "@tanstack/react-router";
import {
  AdminUsersRoute,
  AppLayout,
  ChatRoute,
  ConfirmEmailRoute,
  DataRoute,
  FavoritesRoute,
  HomeRoute,
  LoginRoute,
  MapRoute,
  SignupRoute,
  UnknownPageView,
} from "./routes";
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
const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/login",
  component: LoginRoute,
});
const signupRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/signup",
  component: SignupRoute,
});
const confirmEmailRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/confirm-email",
  component: ConfirmEmailRoute,
});
const favoritesRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/favorites",
  component: FavoritesRoute,
});
const adminUsersRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/admin/users",
  component: AdminUsersRoute,
});

const routeTree = rootRoute.addChildren([
  homeRoute,
  chatRoute,
  mapRoute,
  dataRoute,
  loginRoute,
  signupRoute,
  confirmEmailRoute,
  favoritesRoute,
  adminUsersRoute,
]);

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
