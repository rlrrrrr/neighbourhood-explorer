import { DiaGradient } from "./dia-gradient";

export function SiteFooter() {
  return (
    <footer className="relative isolate mt-auto min-h-[max(clamp(360px,52svh,560px),16vw)] overflow-hidden">
      <div className="absolute inset-0">
        <DiaGradient />
      </div>
      <div className="relative z-10 flex flex-wrap items-center justify-between gap-2 px-4 py-4 sm:px-8">
        <p className="rounded-full bg-surface px-3 py-1.5 text-xs text-text shadow-elevated">
          © 2026 <fbt desc="Product name shown in the footer">Neighbourhood Explorer</fbt>
        </p>
        <p className="rounded-full bg-surface px-3 py-1.5 text-xs text-text shadow-elevated">
          <fbt desc="Credit introducing the authors of the gradient decoration at the bottom of the page">
            Gradient : Dia gradient, conçu par
          </fbt>{" "}
          <a
            href="https://x.com/SebastianDahiya"
            target="_blank"
            rel="noreferrer"
            className="underline-offset-2 hover:text-text hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
          >
            Sebastian Dahiya
          </a>
          <span aria-hidden="true"> · </span>
          <a
            href="https://lab.anishfn.ink/dia-gradient"
            target="_blank"
            rel="noreferrer"
            className="underline-offset-2 hover:text-text hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-action"
          >
            lab.anishfn.ink
          </a>
        </p>
      </div>
    </footer>
  );
}
