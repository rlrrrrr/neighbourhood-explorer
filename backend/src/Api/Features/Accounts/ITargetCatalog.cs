namespace NeighbourhoodExplorer.Api.Features.Accounts;

public interface ITargetCatalog
{
    bool TryGet(FavoriteTargetKey key, out FavoriteTarget? target);

    IEnumerable<FavoriteTarget> All();
}
