import { useNavigate } from "@tanstack/react-router";
import { fbs } from "fbtee";
import { Star } from "lucide-react";
import { Button } from "../../components/ui/button";
import { useSession } from "../accounts/account-queries";

interface FavoriteWidgetViewProps {
  signedIn: boolean;
  onOpenFavorites: () => void;
}

export function FavoriteWidgetView({ signedIn, onOpenFavorites }: FavoriteWidgetViewProps) {
  if (!signedIn) {
    return null;
  }

  return (
    <Button
      variant="ghost"
      size="icon"
      className="shrink-0"
      aria-label={fbs("Favoris", "Accessibility label of the header favorites button")}
      onClick={onOpenFavorites}
    >
      <Star aria-hidden="true" className="size-5" />
    </Button>
  );
}

export function FavoriteWidget() {
  const navigate = useNavigate();
  const sessionQuery = useSession();

  return (
    <FavoriteWidgetView
      signedIn={sessionQuery.data != null}
      onOpenFavorites={() => {
        void navigate({ to: "/favorites" });
      }}
    />
  );
}
