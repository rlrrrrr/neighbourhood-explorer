import { useEffect, useRef, useState, type ReactNode } from "react";
import type { TranslatedString } from "fbtee";
import { Button } from "../components/ui/button";
import { cn } from "../components/ui/cn";

interface HeaderMenuProps {
  id: string;
  menuLabel: TranslatedString;
  trigger: ReactNode;
  panelClassName?: string;
  children: (close: () => void) => ReactNode;
}

export function HeaderMenu({ id, menuLabel, trigger, panelClassName, children }: HeaderMenuProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    function handlePointerDown(event: PointerEvent): void {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === "Escape") {
        setOpen(false);
        triggerRef.current?.focus();
      }
    }
    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  return (
    <div ref={containerRef} className="relative">
      <Button
        ref={triggerRef}
        variant="ghost"
        size="sm"
        aria-expanded={open}
        aria-controls={id}
        onClick={() => setOpen((current) => !current)}
      >
        {trigger}
      </Button>
      <div
        id={id}
        role="group"
        aria-label={menuLabel}
        data-open={open ? "" : undefined}
        inert={!open}
        className={cn(
          "anchored-panel material-panel absolute end-0 z-20 mt-2 grid gap-1 rounded-2xl p-2 shadow-elevated",
          panelClassName,
        )}
      >
        {children(() => setOpen(false))}
      </div>
    </div>
  );
}
