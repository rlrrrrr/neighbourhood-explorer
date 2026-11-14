import { useLocaleContext } from "fbtee";
import type { Criteria } from "../criteria/criteria";
import { DataTableView } from "./data-table-view";
import { useDataTable } from "./use-data-table";
import type { TableSort } from "./table-sort";

const ISTANBUL_CITY_ID = 34;

interface DataViewProps {
  criteria: Criteria;
  onRemoveCriterion: (metricKey: string) => void;
  sort: TableSort | null;
  onSortChange: (sort: TableSort) => void;
  onShowOnMap: (neighborhoodId: number) => void;
}

export function DataView({
  criteria,
  onRemoveCriterion,
  sort,
  onSortChange,
  onShowOnMap,
}: DataViewProps) {
  const { locale } = useLocaleContext();
  const { state, reload } = useDataTable({ cityId: ISTANBUL_CITY_ID });

  return (
    <DataTableView
      criteria={criteria}
      state={state}
      sort={sort}
      locale={locale}
      onRemoveCriterion={onRemoveCriterion}
      onSortChange={onSortChange}
      onShowOnMap={onShowOnMap}
      onReload={reload}
    />
  );
}
