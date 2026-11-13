import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  deleteFavorite,
  getSession,
  listFavorites,
  login,
  logout,
  putFavorite,
  registerAccount,
  confirmEmailAddress,
  ApiError,
} from "../../api/api-client";
import type { AccountSession, FavoriteItem } from "../../api/account-schemas";
import {
  mergeFavoriteIds,
  readLocalFavoriteIds,
  writeLocalFavoriteIds,
} from "../favorites/local-selection";

export const accountQueryKeys = {
  session: ["account", "session"] as const,
  favorites: ["account", "favorites"] as const,
};

export function useSession() {
  return useQuery({
    queryKey: accountQueryKeys.session,
    queryFn: getSession,
    retry: false,
    staleTime: 60_000,
  });
}

export function useFavorites(session: AccountSession | null | undefined) {
  return useQuery({
    queryKey: accountQueryKeys.favorites,
    queryFn: listFavorites,
    enabled: Boolean(session),
  });
}

export function useRegisterMutation() {
  return useMutation({ mutationFn: registerAccount });
}

export function useConfirmEmailMutation() {
  return useMutation({ mutationFn: confirmEmailAddress });
}

export async function syncLocalFavorites(): Promise<readonly string[] | null> {
  const localIds = readLocalFavoriteIds();
  if (localIds.length === 0) {
    return null;
  }

  let remoteIds: readonly string[];
  try {
    const remoteFavorites = await listFavorites();
    remoteIds = remoteFavorites.map((favorite) => favorite.targetId);
  } catch {
    return null;
  }

  const { uploadIds } = mergeFavoriteIds({ remoteIds, localIds });
  for (const targetId of uploadIds) {
    try {
      await putFavorite(targetId);
    } catch (error) {
      if (!(error instanceof ApiError && error.code === "favorite_target_unknown")) {
        return null;
      }
    }
  }

  writeLocalFavoriteIds([]);
  return remoteIds;
}

export function useLoginMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: login,
    onSuccess: async (session) => {
      queryClient.setQueryData(accountQueryKeys.session, session);
      await syncLocalFavorites();
      await queryClient.invalidateQueries({ queryKey: accountQueryKeys.favorites });
    },
  });
}

export function useLogoutMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(accountQueryKeys.session, null);
      queryClient.removeQueries({ queryKey: accountQueryKeys.favorites });
    },
  });
}

export function useSaveFavoriteMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (targetId: string) => {
      await putFavorite(targetId);
      return targetId;
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: accountQueryKeys.favorites });
    },
  });
}

export function useRemoveFavoriteMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (targetId: string) => {
      await deleteFavorite(targetId);
      return targetId;
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: accountQueryKeys.favorites });
    },
  });
}

export function useFavoriteIds(session: AccountSession | null | undefined): {
  favorites: readonly FavoriteItem[];
  isFavorite: (targetId: string) => boolean;
} {
  const favoritesQuery = useFavorites(session);
  const favorites = favoritesQuery.data ?? [];
  const favoriteIds = new Set(favorites.map((favorite) => favorite.targetId));
  return {
    favorites,
    isFavorite: (targetId: string) => favoriteIds.has(targetId),
  };
}
