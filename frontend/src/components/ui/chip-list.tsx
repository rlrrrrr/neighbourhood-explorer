import type { ReactNode } from "react";
import { cn } from "./cn";

export interface Chip {
  key: string;
  label: ReactNode;
}

interface ChipListProps {
  chips: readonly Chip[];
  removeLabel: string;
  onRemove: (key: string) => void;
  className?: string;
}

export function ChipList({ chips, removeLabel, onRemove, className }: ChipListProps) {
  return (
    <ul className={cn("flex flex-wrap gap-2", className)}>
      {chips.map((chip) => (
        <li
          key={chip.key}
          className="flex min-h-9 items-center gap-1 rounded-full border border-border bg-highlight pr-1 pl-3 text-xs font-semibold text-highlight-text"
        >
          {chip.label}
          <button
            type="button"
            aria-label={removeLabel}
            onClick={() => onRemove(chip.key)}
            className="flex size-7 cursor-pointer items-center justify-center rounded-full transition-colors duration-150 ease-out hover:bg-highlight-text/10 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
          >
            <svg
              viewBox="0 0 16 16"
              className="size-3"
              aria-hidden="true"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
            >
              <path d="M4 4l8 8M12 4l-8 8" />
            </svg>
          </button>
        </li>
      ))}
    </ul>
  );
}
