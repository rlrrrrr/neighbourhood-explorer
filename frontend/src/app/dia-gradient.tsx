import { useEffect, useRef } from "react";
import {
  columnScale,
  isSpringSettled,
  peakFromPointer,
  PEAK_SPRING,
  RAISED_SPRING,
  stepSpring,
  type SpringState,
} from "./dia-gradient-motion";

const DEFAULT_COLUMN_COUNT = 11;
const ROW_HEIGHT_FRACTION = 172 / 460;
const BLUR_PER_COLUMN = 10;

export function DiaGradient({ columns = DEFAULT_COLUMN_COUNT }: { columns?: number }) {
  const stageRef = useRef<HTMLDivElement>(null);
  const rowRef = useRef<HTMLDivElement>(null);
  const columnRefs = useRef<Array<HTMLDivElement | null>>([]);

  useEffect(() => {
    const stage = stageRef.current;
    const row = rowRef.current;
    if (!stage || !row) {
      return;
    }

    const span = (columns - 1) / 2;
    let peakTarget = span;
    let peak: SpringState = { position: span, velocity: 0 };
    let raised: SpringState = { position: 0, velocity: 0 };
    let animationFrame = 0;
    let lastTime: number | null = null;
    let running = false;

    const render = () => {
      columnRefs.current.forEach((column, index) => {
        if (column) {
          const scale = columnScale({
            index,
            peak: peak.position,
            raised: raised.position,
            span,
          });
          column.style.transform = `scaleY(${scale})`;
        }
      });
    };

    const tick = (time: number) => {
      const deltaSeconds = lastTime === null ? 0 : Math.min((time - lastTime) / 1000, 1 / 30);
      lastTime = time;
      peak = stepSpring({ state: peak, target: peakTarget, spring: PEAK_SPRING, deltaSeconds });
      raised = stepSpring({ state: raised, target: 1, spring: RAISED_SPRING, deltaSeconds });
      render();
      if (
        isSpringSettled({ state: peak, target: peakTarget }) &&
        isSpringSettled({ state: raised, target: 1 })
      ) {
        running = false;
        lastTime = null;
        return;
      }
      animationFrame = window.requestAnimationFrame(tick);
    };

    const start = () => {
      if (running) {
        return;
      }
      running = true;
      lastTime = null;
      animationFrame = window.requestAnimationFrame(tick);
    };

    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      raised = { position: 1, velocity: 0 };
      render();
      return;
    }

    start();

    const resetPeak = () => {
      peakTarget = span;
      start();
    };

    const trackPointer = (event: PointerEvent) => {
      const stageBounds = stage.getBoundingClientRect();
      const insideStage =
        event.clientX >= stageBounds.left &&
        event.clientX <= stageBounds.right &&
        event.clientY >= stageBounds.top &&
        event.clientY <= stageBounds.bottom;
      if (!insideStage) {
        resetPeak();
        return;
      }
      const rowBounds = row.getBoundingClientRect();
      peakTarget = peakFromPointer({
        pointerX: event.clientX,
        rowLeft: rowBounds.left,
        rowWidth: rowBounds.width,
        columnCount: columns,
      });
      start();
    };

    const releasePointer = (event: PointerEvent) => {
      if (event.pointerType !== "mouse") {
        resetPeak();
      }
    };

    window.addEventListener("pointermove", trackPointer, { passive: true });
    window.addEventListener("pointerdown", trackPointer, { passive: true });
    window.addEventListener("pointerup", releasePointer);
    window.addEventListener("pointercancel", resetPeak);
    window.addEventListener("blur", resetPeak);
    document.documentElement.addEventListener("mouseleave", resetPeak);

    return () => {
      window.cancelAnimationFrame(animationFrame);
      window.removeEventListener("pointermove", trackPointer);
      window.removeEventListener("pointerdown", trackPointer);
      window.removeEventListener("pointerup", releasePointer);
      window.removeEventListener("pointercancel", resetPeak);
      window.removeEventListener("blur", resetPeak);
      document.documentElement.removeEventListener("mouseleave", resetPeak);
    };
  }, [columns]);

  return (
    <div
      ref={stageRef}
      aria-hidden="true"
      className="pointer-events-none relative size-full select-none overflow-hidden [container-type:size]"
    >
      <div
        ref={rowRef}
        className="absolute left-1/2 flex origin-bottom items-end [--bleed:4cqw] [--row:62cqh]"
        style={{
          bottom: "calc(var(--bleed) * -1)",
          width: "calc(50cqw + var(--bleed))",
          transform: "translateX(-50%) scale(2)",
          filter: `blur(calc((50cqw + var(--bleed)) / ${BLUR_PER_COLUMN * columns}))`,
        }}
      >
        {Array.from({ length: columns }, (_, index) => (
          <div
            key={index}
            ref={(column) => {
              columnRefs.current[index] = column;
            }}
            className="will-change-transform flex-1 bg-no-repeat"
            style={{
              height: `calc((var(--row) * ${ROW_HEIGHT_FRACTION} + var(--bleed)) / 2)`,
              backgroundColor: "var(--ramp-5)",
              backgroundImage:
                "linear-gradient(180deg, var(--highlight), var(--ramp-1) 25%, var(--ramp-2) 50%, var(--ramp-3) 75%, var(--ramp-5))",
              backgroundSize: "100% calc(100% - var(--bleed) / 2)",
              transformOrigin: "50% calc(100% - var(--bleed) / 2)",
            }}
          />
        ))}
      </div>
    </div>
  );
}
