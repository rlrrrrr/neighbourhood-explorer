import { useEffect, useRef, useState, type ComponentProps } from "react";
import { cn } from "./cn";

type FeatheredImageProps = ComponentProps<"img">;

export function FeatheredImage({ className, onError, onLoad, ...props }: FeatheredImageProps) {
  const [loaded, setLoaded] = useState(false);
  const imageRef = useRef<HTMLImageElement | null>(null);

  useEffect(() => {
    if (imageRef.current?.complete) {
      setLoaded(true);
    }
  }, []);

  return (
    <span className={cn("relative block", className)}>
      <span
        aria-hidden="true"
        className={cn(
          "feathered-image skeleton pointer-events-none absolute inset-0",
          loaded && "opacity-0 transition-opacity duration-300 motion-reduce:transition-none",
        )}
      />
      <img
        ref={imageRef}
        onLoad={(event) => {
          setLoaded(true);
          onLoad?.(event);
        }}
        onError={(event) => {
          setLoaded(true);
          onError?.(event);
        }}
        className={cn(
          "feathered-image block h-full w-full object-cover transition-[opacity,filter] duration-500 ease-out motion-reduce:transition-none",
          loaded ? "opacity-100 blur-none" : "opacity-0 blur-[6px]",
        )}
        {...props}
      />
    </span>
  );
}
