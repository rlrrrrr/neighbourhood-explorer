export interface SpringSpec {
  stiffness: number;
  damping: number;
  mass: number;
}

export interface SpringState {
  position: number;
  velocity: number;
}

export const PEAK_SPRING: SpringSpec = { stiffness: 170, damping: 24, mass: 0.8 };
export const RAISED_SPRING: SpringSpec = { stiffness: 220, damping: 26, mass: 1 };

const PEAK_SCALE_BOOST = 260 / 172;
const SETTLED_DISTANCE = 0.001;
const SETTLED_VELOCITY = 0.01;

export function stepSpring({
  state,
  target,
  spring,
  deltaSeconds,
}: {
  state: SpringState;
  target: number;
  spring: SpringSpec;
  deltaSeconds: number;
}): SpringState {
  const acceleration =
    (spring.stiffness * (target - state.position) - spring.damping * state.velocity) / spring.mass;
  const velocity = state.velocity + acceleration * deltaSeconds;
  return { position: state.position + velocity * deltaSeconds, velocity };
}

export function isSpringSettled({
  state,
  target,
}: {
  state: SpringState;
  target: number;
}): boolean {
  return (
    Math.abs(state.velocity) < SETTLED_VELOCITY &&
    Math.abs(state.position - target) < SETTLED_DISTANCE
  );
}

export function columnScale({
  index,
  peak,
  raised,
  span,
}: {
  index: number;
  peak: number;
  raised: number;
  span: number;
}): number {
  if (span === 0) {
    return 1 + PEAK_SCALE_BOOST * raised;
  }
  const proximity = Math.max(0, 1 - Math.abs(index - peak) / span);
  return 1 + PEAK_SCALE_BOOST * raised * proximity;
}

export function peakFromPointer({
  pointerX,
  rowLeft,
  rowWidth,
  columnCount,
}: {
  pointerX: number;
  rowLeft: number;
  rowWidth: number;
  columnCount: number;
}): number {
  const position = ((pointerX - rowLeft) / rowWidth) * columnCount - 0.5;
  return Math.min(Math.max(position, 0), columnCount - 1);
}
