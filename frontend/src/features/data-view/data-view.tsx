import { useLocaleContext } from "fbtee";
import { useCity } from "../metrics/use-city";
import type { Criteria } from "../criteria/criteria";
import { DataTableView } from "./data-table-view";
import { useDataTable, type DataTableState } from "./use-data-table";
import type { TableSort } from "./table-sort";

interface DataViewProps {
  criteria: Criteria;
  onRemoveCriterion: (metricKey: string) => void;
  sort: TableSort | null;
  onSortChange: (sort: TableSort | null) => void;
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
  const { state: city, reload: reloadCity } = useCity();

  const { state, reload } = useDataTable({
    cityId: city.status === "ready" ? city.city.plateCode : null,
  });

  const viewState: DataTableState =
    city.status === "error" ? { status: "error", code: city.code } : state;

  return (
    <DataTableView
      criteria={criteria}
      state={viewState}
      sort={sort}
      locale={locale}
      onRemoveCriterion={onRemoveCriterion}
      onSortChange={onSortChange}
      onShowOnMap={onShowOnMap}
      onReload={() => {
        reloadCity();
        reload();
      }}
    />
  );
}
