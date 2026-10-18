export type SortDirection = "ascending" | "descending";

export interface TableSort {
  key: string;
  direction: SortDirection;
}

export function nextSort(current: TableSort | null, key: string): TableSort {
  if (current?.key !== key) {
    return { key, direction: "ascending" };
  }
  return { key, direction: current.direction === "ascending" ? "descending" : "ascending" };
}

export function sortRows<Row>({
  rows,
  sort,
  sortValueOf,
}: {
  rows: readonly Row[];
  sort: TableSort | null;
  sortValueOf: (row: Row, key: string) => number;
}): Row[] {
  if (!sort) {
    return [...rows];
  }
  const factor = sort.direction === "ascending" ? 1 : -1;
  return rows
    .map((row, index) => ({ row, index }))
    .sort((left, right) => {
      const difference = sortValueOf(left.row, sort.key) - sortValueOf(right.row, sort.key);
      return difference === 0 ? left.index - right.index : difference * factor;
    })
    .map(({ row }) => row);
}
