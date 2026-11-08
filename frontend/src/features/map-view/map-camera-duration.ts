const MAP_CAMERA_ANIMATION_MS = 700;

export function mapCameraAnimationDuration(): number {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches
    ? 0
    : MAP_CAMERA_ANIMATION_MS;
}
