using NeighbourhoodExplorer.Etl.Ibb;

namespace NeighbourhoodExplorer.Etl.Tests;

public class IbbTextRepairTests
{
    [Theory]
    [InlineData("ZEYTÝNBURNU", "ZEYTİNBURNU")]
    [InlineData("YEÞÝLTEPE", "YEŞİLTEPE")]
    [InlineData("CAFERAÐA", "CAFERAĞA")]
    [InlineData("PÝRÝPAÞA", "PİRİPAŞA")]
    [InlineData("KADIKÖY", "KADIKÖY")]
    [InlineData("ADALAR", "ADALAR")]
    public void Repair_decodes_windows_1254_bytes_read_as_latin1(string value, string expected) =>
        Assert.Equal(expected, IbbTextRepair.Repair(value));
}
