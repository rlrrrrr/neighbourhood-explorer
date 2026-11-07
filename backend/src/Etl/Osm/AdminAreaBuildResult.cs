namespace NeighbourhoodExplorer.Etl.Osm;

public sealed record NameIssue(string District, string Name, string RelationId);

public sealed record DuplicateName(string Name, int Count, IReadOnlyList<string> Districts);

public sealed record GeometryIssue(string RelationId, string Name, int AdminLevel);

public sealed record UnassignedNeighborhood(string RelationId, string Name);

public sealed record AmbiguousNeighborhood(string RelationId, string Name, IReadOnlyList<string> Districts);

public sealed record OutsideProvinceArea(int AdminLevel, string RelationId, string Name);

public sealed record AdminAreaBuildResult(
    long ProvinceId,
    string ProvinceName,
    string CountryCode,
    int ProvinceCount,
    int DistrictCount,
    int NeighborhoodCount,
    IReadOnlyList<NameIssue> NamesNotMatchingPattern,
    IReadOnlyList<DuplicateName> DuplicateNames,
    IReadOnlyList<DuplicateName> DuplicateNamesWithinDistrict,
    IReadOnlyList<GeometryIssue> InvalidGeometries,
    IReadOnlyList<UnassignedNeighborhood> UnassignedNeighborhoods,
    IReadOnlyList<AmbiguousNeighborhood> AmbiguousNeighborhoods,
    IReadOnlyList<OutsideProvinceArea> OutsideProvinceAreas);

public sealed record AdminAreaBuildRequest(string SourceId, string ProvinceCode);
