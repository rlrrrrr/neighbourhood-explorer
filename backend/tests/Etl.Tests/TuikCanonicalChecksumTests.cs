using NeighbourhoodExplorer.Etl.Tuik;

namespace NeighbourhoodExplorer.Etl.Tests;

public class TuikCanonicalChecksumTests
{
    [Fact]
    public void Compute_is_stable_across_dictionary_order()
    {
        var first = new Dictionary<string, int> { ["TR100"] = 15754053, ["1103"] = 17489 };
        var second = new Dictionary<string, int> { ["1103"] = 17489, ["TR100"] = 15754053 };

        Assert.Equal(TuikCanonicalChecksum.Compute(first), TuikCanonicalChecksum.Compute(second));
    }

    [Fact]
    public void Compute_matches_the_documented_canonical_form()
    {
        var values = new Dictionary<string, int> { ["TR100"] = 15754053, ["1103"] = 17489 };

        Assert.Equal(
            "5e3e01a1329e60099c3f831efb384e5218565439e86d19a6253dc25e8ec2b282",
            TuikCanonicalChecksum.Compute(values));
    }

    [Fact]
    public void Compute_changes_when_a_value_changes()
    {
        var original = new Dictionary<string, int> { ["1103"] = 17489 };
        var revised = new Dictionary<string, int> { ["1103"] = 17490 };

        Assert.NotEqual(TuikCanonicalChecksum.Compute(original), TuikCanonicalChecksum.Compute(revised));
    }
}
