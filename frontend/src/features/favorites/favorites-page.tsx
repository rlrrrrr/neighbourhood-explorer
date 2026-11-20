import type { ReactNode } from "react";
import { Button } from "../../components/ui/button";
import type { FavoriteItem } from "../../api/account-schemas";

interface FavoritesPageProps {
  signedIn: boolean;
  isLoading: boolean;
  items: readonly FavoriteItem[];
  removingId: string | null;
  errorMessage: ReactNode;
  onRemove: (targetId: string) => void;
  onShowOnMap: (targetId: string) => void;
  onSignIn: () => void;
  onExploreMap: () => void;
}

export function FavoritesPage({
  signedIn,
  isLoading,
  items,
  removingId,
  errorMessage,
  onRemove,
  onShowOnMap,
  onSignIn,
  onExploreMap,
}: FavoritesPageProps) {
  return (
    <section className="mx-auto w-full max-w-2xl flex-1 px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight">
        <fbt desc="Title of the favorites screen">Favoris</fbt>
      </h1>
      <p className="mt-2 text-sm text-text-secondary">
        <fbt desc="Subtitle of the favorites screen">
          Les quartiers que tu as enregistrés. Ils te suivent d'un appareil à l'autre après
          connexion.
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
            <fbt desc="Message shown on the favorites screen when signed out">
              Connecte-toi pour retrouver les favoris de ton compte. Tes enregistrements locaux
              seront fusionnés à la première connexion.
            </fbt>
          </p>
          <Button onClick={onSignIn}>
            <fbt desc="Button opening the login page from the favorites screen">Se connecter</fbt>
          </Button>
        </div>
      )}

      {isLoading && (
        <p aria-live="polite" className="mt-8 text-sm text-text-secondary">
          <fbt desc="Loading message on the favorites screen">Chargement des favoris…</fbt>
        </p>
      )}

      {signedIn && !isLoading && items.length === 0 && (
        <div className="mt-8 grid justify-items-start gap-3 rounded-2xl bg-surface p-5 shadow-elevated">
          <p className="text-sm text-text-secondary">
            <fbt desc="Empty state of the favorites screen">
              Aucun favori pour l'instant. Enregistre un quartier depuis le chat ou la carte.
            </fbt>
          </p>
          <Button variant="outline" onClick={onExploreMap}>
            <fbt desc="Button opening the map from the empty favorites screen">
              Explorer la carte
            </fbt>
          </Button>
        </div>
      )}

      {signedIn && !isLoading && items.length > 0 && (
        <ul className="mt-8 grid gap-3">
          {items.map((favorite) => (
            <li
              key={`${favorite.targetType}:${favorite.targetId}`}
              className="grain flex flex-wrap items-center gap-3 rounded-2xl bg-surface p-4 shadow-elevated"
            >
              <div className="min-w-0 flex-1">
                <p className="font-semibold">{favorite.name ?? favorite.targetId}</p>
                {favorite.district && (
                  <p className="text-xs text-text-secondary">{favorite.district}</p>
                )}
              </div>
              <Button
                variant="outline"
                size="sm"
                disabled={removingId === favorite.targetId}
                onClick={() => onRemove(favorite.targetId)}
              >
                {removingId === favorite.targetId ? (
                  <fbt desc="Remove favorite button while the request is running">Retrait…</fbt>
                ) : (
                  <fbt desc="Remove favorite button">Retirer</fbt>
                )}
              </Button>
              <Button variant="ghost" size="sm" onClick={() => onShowOnMap(favorite.targetId)}>
                <fbt desc="Button opening the map from a favorite row">Voir sur la carte</fbt>
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
