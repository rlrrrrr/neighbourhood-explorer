import {
  DEFAULT_WEIGHT,
  EMPTY_CRITERIA,
  isMetricKey,
  type Criteria,
  type Criterion,
} from "./criteria";

export const MAX_CRITERIA_LENGTH = 400;
export const MAX_CRITERIA_COUNT = 24;

const ITEM_SEPARATOR = ",";
const WEIGHT_SEPARATOR = ":";

export function encodeCriteria(criteria: Criteria): string | undefined {
  if (criteria.weights.length === 0) {
    return undefined;
  }
  return criteria.weights
    .map((criterion) =>
      criterion.weight === DEFAULT_WEIGHT
        ? criterion.metricKey
        : `${criterion.metricKey}${WEIGHT_SEPARATOR}${criterion.weight}`,
    )
    .join(ITEM_SEPARATOR);
}

function decodeCriterion(item: string): Criterion | undefined {
  const parts = item.split(WEIGHT_SEPARATOR);
  if (parts.length > 2) {
    return undefined;
  }
  const [metricKey, weightText] = parts;
  if (!isMetricKey(metricKey)) {
    return undefined;
  }
  if (weightText === undefined) {
    return { metricKey, weight: DEFAULT_WEIGHT };
  }
  const weight = Number(weightText);
  if (!Number.isFinite(weight) || weight <= 0 || weight > DEFAULT_WEIGHT) {
    return undefined;
  }
  return { metricKey, weight: Math.round(weight * 100) / 100 };
}

export function decodeCriteria(value: unknown): Criteria {
  if (typeof value !== "string") {
    return EMPTY_CRITERIA;
  }
  const text = value.trim();
  if (text.length === 0 || text.length > MAX_CRITERIA_LENGTH) {
    return EMPTY_CRITERIA;
  }
  const weights: Criterion[] = [];
  for (const item of text.split(ITEM_SEPARATOR)) {
    if (weights.length === MAX_CRITERIA_COUNT) {
      break;
    }
    const criterion = decodeCriterion(item.trim());
    if (criterion && !weights.some((existing) => existing.metricKey === criterion.metricKey)) {
      weights.push(criterion);
    }
  }
  return weights.length === 0 ? EMPTY_CRITERIA : { weights };
}
