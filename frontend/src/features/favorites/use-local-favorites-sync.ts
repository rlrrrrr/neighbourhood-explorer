import { useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import type { AccountSession } from "../../api/account-schemas";
import { accountQueryKeys, syncLocalFavorites } from "../accounts/account-queries";
import { readLocalFavoriteIds } from "./local-selection";

export function useLocalFavoritesSync(session: AccountSession | null | undefined): void {
  const queryClient = useQueryClient();
  const isSyncing = useRef(false);

  useEffect(() => {
    if (!session || isSyncing.current || readLocalFavoriteIds().length === 0) {
      return;
    }

    isSyncing.current = true;
    void syncLocalFavorites()
      .then(async () => {
        await queryClient.invalidateQueries({ queryKey: accountQueryKeys.favorites });
      })
      .finally(() => {
        isSyncing.current = false;
      });
  }, [session, queryClient]);
}
