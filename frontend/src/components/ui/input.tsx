import type { ComponentProps } from "react";
import { cn } from "./cn";

export function Input({ className, type, ...props }: ComponentProps<"input">) {
  return (
    <input
      type={type}
      data-slot="input"
      className={cn(
        "h-9 w-full min-w-0 rounded-lg border border-border bg-transparent px-3 py-1 text-sm text-text transition-[border-color] duration-150 ease-out placeholder:text-text-secondary focus-visible:border-action focus-visible:outline-none disabled:pointer-events-none disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}
