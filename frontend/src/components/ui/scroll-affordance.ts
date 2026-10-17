import { useCallback, useEffect, useState } from "react";

type ScrollAxis = "vertical" | "horizontal";

export function hasMoreToScroll({
  offset,
  viewport,
  content,
}: {
  offset: number;
  viewport: number;
  content: number;
}): boolean {
  return offset + viewport < content - 1;
}

export function hasOverflow({ viewport, content }: { viewport: number; content: number }): boolean {
  return content > viewport + 1;
}

function readMetrics(element: HTMLElement, axis: ScrollAxis) {
  return axis === "vertical"
    ? { offset: element.scrollTop, viewport: element.clientHeight, content: element.scrollHeight }
    : { offset: element.scrollLeft, viewport: element.clientWidth, content: element.scrollWidth };
}

export function useScrollOverflowHint({ axis }: { axis: ScrollAxis }): {
  containerRef: (element: HTMLElement | null) => void;
  hasMore: boolean;
  overflows: boolean;
} {
  const [element, setElement] = useState<HTMLElement | null>(null);
  const [state, setState] = useState({ hasMore: false, overflows: false });

  const containerRef = useCallback((node: HTMLElement | null) => {
    setElement(node);
  }, []);

  useEffect(() => {
    if (!element) {
      return;
    }
    const update = () => {
      const metrics = readMetrics(element, axis);
      const hasMore = hasMoreToScroll(metrics);
      const overflows = hasOverflow(metrics);
      setState((current) =>
        current.hasMore === hasMore && current.overflows === overflows
          ? current
          : { hasMore, overflows },
      );
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(element);
    for (const child of Array.from(element.children)) {
      observer.observe(child);
    }
    element.addEventListener("scroll", update, { passive: true });
    return () => {
      observer.disconnect();
      element.removeEventListener("scroll", update);
    };
  }, [element, axis]);

  return { containerRef, hasMore: state.hasMore, overflows: state.overflows };
}
