using System.Text.Json;

namespace NeighbourhoodExplorer.Api.Features.Accounts;

public sealed class FixtureTargetCatalog : ITargetCatalog
{
    private readonly IReadOnlyDictionary<FavoriteTargetKey, FavoriteTarget> targets;

    public FixtureTargetCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Accounts", "neighborhood-targets.json");
        var document = JsonSerializer.Deserialize<NeighborhoodTargetDocument>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The neighborhood target catalog is empty.");

        targets = document.Items.ToDictionary(
            item => new FavoriteTargetKey(document.TargetType, item.TargetId),
            item => new FavoriteTarget(document.TargetType, item.TargetId, item.Name, item.Ilce));
    }

    public bool TryGet(FavoriteTargetKey key, out FavoriteTarget? target) =>
        targets.TryGetValue(key, out target);

    public IEnumerable<FavoriteTarget> All() => targets.Values;

    private sealed record NeighborhoodTargetDocument(string TargetType, IReadOnlyList<NeighborhoodTargetItem> Items);

    private sealed record NeighborhoodTargetItem(string TargetId, string Ilce, string Name);
}
