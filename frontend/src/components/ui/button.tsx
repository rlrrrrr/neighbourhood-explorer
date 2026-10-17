import type { ComponentProps } from "react";
import { tv, type VariantProps } from "tailwind-variants";
import { cn } from "./cn";

const buttonVariants = tv({
  base: "inline-flex cursor-pointer items-center justify-center gap-2 rounded-full font-semibold transition-[color,background-color,border-color,opacity,transform] duration-200 ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action disabled:cursor-not-allowed disabled:opacity-50 motion-safe:active:scale-[0.97]",
  variants: {
    variant: {
      primary: "bg-action text-action-text hover:opacity-90",
      outline: "border border-border bg-transparent text-text hover:bg-surface",
      ghost: "bg-transparent text-text hover:bg-surface",
    },
    size: {
      sm: "min-h-9 px-4 text-sm",
      md: "min-h-11 px-5 text-sm",
      lg: "min-h-12 px-7 text-base",
      icon: "size-9",
    },
  },
  defaultVariants: {
    variant: "primary",
    size: "md",
  },
});

type ButtonProps = ComponentProps<"button"> & VariantProps<typeof buttonVariants>;

export function Button({ className, variant, size, type, ...props }: ButtonProps) {
  return (
    <button
      type={type ?? "button"}
      className={cn(buttonVariants({ variant, size }), className)}
      {...props}
    />
  );
}
