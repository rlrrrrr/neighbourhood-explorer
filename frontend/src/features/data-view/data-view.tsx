import { Button } from "../../components/ui/button";
import { SAMPLE_NEIGHBORHOODS } from "../neighborhood-detail/sample-neighborhoods";

interface DataViewProps {
  onShowOnMap: () => void;
}

export function DataView({ onShowOnMap }: DataViewProps) {
  return (
    <div className="mx-auto w-full max-w-4xl flex-1 px-4 py-12 sm:px-8">
      <h1 className="text-2xl font-bold tracking-tight">
        <fbt desc="Title of the data view">Valeurs brutes</fbt>
      </h1>
      <p className="mt-2 text-sm text-text-secondary">
        <fbt desc="Subtitle of the data view, with the year and the raw-value promise">
          Millésime 2024 · valeurs brutes avec unité, sans score composite
        </fbt>
      </p>
      <div className="mt-8 overflow-x-auto rounded-2xl shadow-elevated">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border bg-surface text-left text-xs font-semibold text-text-secondary">
              <th scope="col" className="px-4 py-3">
                <fbt desc="Column header of the neighborhood name">Quartier</fbt>
              </th>
              <th scope="col" className="px-4 py-3">
                <fbt desc="Column header of the district name">District</fbt>
              </th>
              <th scope="col" className="px-4 py-3 text-right">
                <fbt desc="Column header of the cafés per square kilometer">Cafés / km²</fbt>
              </th>
              <th scope="col" className="px-4 py-3 text-right">
                <fbt desc="Column header of the walking distance to the metro">Métro à pied</fbt>
              </th>
              <th scope="col" className="px-4 py-3 text-right">
                <fbt desc="Column header of the green cover">Verdure</fbt>
              </th>
              <th scope="col" className="px-4 py-3">
                <span className="sr-only">
                  <fbt desc="Accessible column header of the map action">Carte</fbt>
                </span>
              </th>
            </tr>
          </thead>
          <tbody>
            {SAMPLE_NEIGHBORHOODS.map((row, index) => (
              <tr key={row.name} className={index === 0 ? "bg-highlight/30" : "odd:bg-surface/60"}>
                <th scope="row" className="px-4 py-3 text-left font-semibold">
                  {row.name}
                </th>
                <td className="px-4 py-3 text-text-secondary">{row.district}</td>
                <td className="px-4 py-3 text-right tabular-nums">{row.cafes}</td>
                <td className="px-4 py-3 text-right tabular-nums">{row.metro}</td>
                <td className="px-4 py-3 text-right tabular-nums">{row.green}</td>
                <td className="px-4 py-3 text-right">
                  <Button variant="ghost" size="sm" onClick={onShowOnMap}>
                    <fbt desc="Button opening the map view from a data row">Voir sur la carte</fbt>
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
