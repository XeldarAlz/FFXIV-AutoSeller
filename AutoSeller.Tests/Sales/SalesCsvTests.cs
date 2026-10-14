using System.Globalization;
using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class SalesCsvTests
{
    private static readonly DateTime SoldAt = new(2026, 10, 14, 9, 30, 5, DateTimeKind.Utc);

    private static readonly FixtureItemNames Names = new(new Dictionary<uint, string>
    {
        [DarksteelOre] = "Darksteel Ore",
        [MythriteIngot] = "Ingot, \"Mythrite\"",
    });

    private static string Export()
    {
        return SalesCsv.Write(
            Of(
                History(SoldAt, DarksteelOre, 10, 1_999) with { ListedAtUtc = SoldAt.AddHours(-26) },
                Diff(SoldAt.AddHours(-8), SoldAt.AddHours(1), MythriteIngot, 5, 900, SecondRetainerId) with { IsHq = true },
                UnmatchedNotice(SoldAt.AddHours(2), CottonBoll, 99, 15, 1_411)).AsSpan(),
            Names);
    }

    [Fact]
    public void RowsCarryTimeItemQualityQuantityPricesAndSource()
    {
        var lines = Export().Split('\n');

        Assert.Equal(5, lines.Length);
        Assert.Equal(SalesCsv.Header, lines[0]);
        Assert.Equal("2026-10-14T09:30:05Z,5057,Darksteel Ore,NQ,10,1999,18991,2026-10-13T07:30:05Z,history,confirmed", lines[1]);
        Assert.Equal("2026-10-14T10:30:05Z,5068,\"Ingot, \"\"Mythrite\"\"\",HQ,5,900,4275,,diff,inferred", lines[2]);
        Assert.Equal("2026-10-14T11:30:05Z,5343,,NQ,99,15,1411,,notice,confirmed", lines[3]);
        Assert.Equal(string.Empty, lines[4]);
    }

    [Fact]
    public void ExportNeverCarriesCharacterOrRetainerIds()
    {
        var csv = Export();

        Assert.DoesNotContain(CharacterId.ToString(CultureInfo.InvariantCulture), csv, StringComparison.Ordinal);
        Assert.DoesNotContain(FirstRetainerId.ToString(CultureInfo.InvariantCulture), csv, StringComparison.Ordinal);
        Assert.DoesNotContain(SecondRetainerId.ToString(CultureInfo.InvariantCulture), csv, StringComparison.Ordinal);
        Assert.DoesNotContain("retainer", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("character", csv, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportIsTheSameInEveryCulture()
    {
        var invariant = Export();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(invariant, Export());
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(invariant, Export());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void EmptyLedgerExportsTheHeaderOnly()
    {
        Assert.Equal(SalesCsv.Header + "\n", SalesCsv.Write([], Names));
    }
}
