namespace NeighbourhoodExplorer.Etl.Database;

public interface IMetricValue
{
    long AdminAreaId { get; }

    double RawValue { get; }

    double NormalizedValue { get; }
}
