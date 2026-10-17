export const DEFAULT_WEIGHT = 1;
export const MAX_WEIGHT = 1;
export const MAX_METRIC_KEY_LENGTH = 32;

const METRIC_KEY_PATTERN = /^[a-z][a-z0-9-]*$/;

export interface Criterion {
  metricKey: string;
  weight: number;
}

export interface Criteria {
  weights: readonly Criterion[];
}

export const EMPTY_CRITERIA: Criteria = { weights: [] };

export function isMetricKey(value: string): boolean {
  return (
    value.length > 0 && value.length <= MAX_METRIC_KEY_LENGTH && METRIC_KEY_PATTERN.test(value)
  );
}

function clampWeight(weight: number): number {
  if (!Number.isFinite(weight)) {
    return 0;
  }
  return Math.round(Math.min(Math.max(weight, 0), MAX_WEIGHT) * 100) / 100;
}

export function weightOf(criteria: Criteria, metricKey: string): number | undefined {
  return criteria.weights.find((criterion) => criterion.metricKey === metricKey)?.weight;
}

export function hasCriterion(criteria: Criteria, metricKey: string): boolean {
  return weightOf(criteria, metricKey) !== undefined;
}

export function withoutCriterion(criteria: Criteria, metricKey: string): Criteria {
  const weights = criteria.weights.filter((criterion) => criterion.metricKey !== metricKey);
  return weights.length === criteria.weights.length ? criteria : { weights };
}

export function withWeight(criteria: Criteria, metricKey: string, weight: number): Criteria {
  if (!isMetricKey(metricKey) || !Number.isFinite(weight)) {
    return criteria;
  }
  const rounded = clampWeight(weight);
  if (rounded === 0) {
    return withoutCriterion(criteria, metricKey);
  }
  if (!hasCriterion(criteria, metricKey)) {
    return { weights: [...criteria.weights, { metricKey, weight: rounded }] };
  }
  if (weightOf(criteria, metricKey) === rounded) {
    return criteria;
  }
  return {
    weights: criteria.weights.map((criterion) =>
      criterion.metricKey === metricKey ? { metricKey, weight: rounded } : criterion,
    ),
  };
}

export function toggleCriterion(criteria: Criteria, metricKey: string): Criteria {
  return hasCriterion(criteria, metricKey)
    ? withoutCriterion(criteria, metricKey)
    : withWeight(criteria, metricKey, DEFAULT_WEIGHT);
}
