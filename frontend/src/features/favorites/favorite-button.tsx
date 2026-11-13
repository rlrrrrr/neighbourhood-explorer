import { useState, type ReactNode } from "react";
import { Button } from "../../components/ui/button";
import { ApiError } from "../../api/api-client";
import { accountErrorMessage } from "../accounts/account-error-message";
import {
  useFavorites,
  useRemoveFavoriteMutation,
  useSaveFavoriteMutation,
  useSession,
} from "../accounts/account-queries";
import { readLocalFavoriteIds, writeLocalFavoriteIds } from "./local-selection";

interface FavoriteButtonProps {
  saved: boolean;
  busy: boolean;
  onToggle: () => void;
}

export function FavoriteButton({ saved, busy, onToggle }: FavoriteButtonProps) {
  return (
    <Button
      variant={saved ? "primary" : "outline"}
      size="sm"
      aria-pressed={saved}
      disabled={busy}
      onClick={onToggle}
    >
      {saved ? (
        <fbt desc="Save button label when the neighborhood is already saved">Enregistré</fbt>
      ) : (
        <fbt desc="Save button label when the neighborhood is not saved">Enregistrer</fbt>
      )}
    </Button>
  );
}

export function SaveNeighborhoodButton({ targetId }: { targetId: string }) {
  const sessionQuery = useSession();
  const session = sessionQuery.data ?? null;
  const favoritesQuery = useFavorites(session);
  const saveMutation = useSaveFavoriteMutation();
  const removeMutation = useRemoveFavoriteMutation();
  const [, refreshLocalIds] = useState(0);
  const [errorMessage, setErrorMessage] = useState<ReactNode>(null);
  const localIds = session === null ? readLocalFavoriteIds() : [];

  const remoteSaved =
    favoritesQuery.data?.some((favorite) => favorite.targetId === targetId) ?? false;
  const saved = session ? remoteSaved : localIds.includes(targetId);
  const busy = saveMutation.isPending || removeMutation.isPending;

  const toggle = async () => {
    setErrorMessage(null);
    if (session) {
      try {
        if (remoteSaved) {
          await removeMutation.mutateAsync(targetId);
        } else {
          await saveMutation.mutateAsync(targetId);
        }
      } catch (error) {
        if (error instanceof ApiError) {
          setErrorMessage(accountErrorMessage({ code: error.code, errors: error.errors }));
        } else {
          setErrorMessage(accountErrorMessage({ code: "unexpected_error", errors: [] }));
        }
      }
      return;
    }

    const nextIds = saved
      ? localIds.filter((favoriteId) => favoriteId !== targetId)
      : [...localIds, targetId];
    writeLocalFavoriteIds(nextIds);
    refreshLocalIds((revision) => revision + 1);
  };

  return (
    <div className="grid justify-items-start gap-1">
      <FavoriteButton saved={saved} busy={busy} onToggle={() => void toggle()} />
      {errorMessage && (
        <p role="alert" className="text-xs font-semibold text-info">
          {errorMessage}
        </p>
      )}
    </div>
  );
}
