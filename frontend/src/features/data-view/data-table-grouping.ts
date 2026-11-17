import type { DataTableRow } from "./data-table-client";
import type { TableSort } from "./table-sort";

export interface DistrictGroup {
  districtName: string;
  count: number;
  average: number | null;
  maximum: number;
}

export function orderGroupedRows({
  rows,
  sort,
  locale,
}: {
  rows: readonly DataTableRow[];
  sort: TableSort | null;
  locale: string;
}): DataTableRow[] {
  return [...rows].sort((left, right) => {
    const districtOrder = left.districtName.localeCompare(right.districtName, locale);

    if (districtOrder !== 0) {
      return districtOrder;
    }

    if (sort !== null) {
      const leftValue = left.values[sort.key]?.raw;
      const rightValue = right.values[sort.key]?.raw;

      if (leftValue === undefined && rightValue !== undefined) {
        return 1;
      }

      if (leftValue !== undefined && rightValue === undefined) {
        return -1;
      }

      if (leftValue !== undefined && rightValue !== undefined && leftValue !== rightValue) {
        return sort.direction === "descending" ? rightValue - leftValue : leftValue - rightValue;
      }
    }

    return left.name.localeCompare(right.name, locale);
  });
}

export function districtGroups({
  rows,
  metricKey,
}: {
  rows: readonly DataTableRow[];
  metricKey: string | undefined;
}): DistrictGroup[] {
  const groups = new Map<
    string,
    { count: number; total: number; measured: number; maximum: number }
  >();

  for (const row of rows) {
    const group = groups.get(row.districtName) ?? { count: 0, total: 0, measured: 0, maximum: 0 };
    group.count += 1;
    const raw = metricKey === undefined ? undefined : row.values[metricKey]?.raw;

    if (raw !== undefined && Number.isFinite(raw)) {
      group.total += raw;
      group.measured += 1;
      group.maximum = Math.max(group.maximum, raw);
    }

    groups.set(row.districtName, group);
  }

  return [...groups].map(([districtName, group]) => ({
    districtName,
    count: group.count,
    average: group.measured === 0 ? null : group.total / group.measured,
    maximum: group.maximum,
  }));
}
