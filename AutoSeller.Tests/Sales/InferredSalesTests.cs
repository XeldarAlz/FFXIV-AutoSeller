using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Sales.Model;
using static AutoSeller.Tests.Sales.SaleFixtures;

namespace AutoSeller.Tests.Sales;

public sealed class InferredSalesTests
{
    [Fact]
    public void EverySoldChangeBecomesAnInferredSaleNetOfTheCityTax()
    {
        var previousRead = Morning;
        var currentRead = Morning.AddHours(8);
        var listedAt = Morning.AddDays(-1);
        var previous = Snapshot(previousRead, 0, Slot(0, DarksteelOre, 10, 200), Slot(1, CottonBoll, 99, 15), Slot(2, MythriteIngot, 5, 900));
        var current = Snapshot(currentRead, 2_000, Slot(2, MythriteIngot, 5, 850));
        SnapshotDiffWrite[] writes =
        [
            Write(SnapshotDiffWriteKind.Listed, listedAt, DarksteelOre, 10, 0, 200),
            Write(SnapshotDiffWriteKind.Returned, previousRead.AddMinutes(1), CottonBoll, 99, 15, 15),
        ];
        var diff = SnapshotDiff.Compute(previous, current, writes);

        var sales = InferredSales.From(diff, CityTaxPercent);

        var sale = Assert.Single(sales);
        Assert.Equal(SaleSource.Diff, sale.Source);
        Assert.Equal(SaleConfidence.Inferred, sale.Confidence);
        Assert.Equal(CharacterId, sale.CharacterContentId);
        Assert.Equal(FirstRetainerId, sale.RetainerId);
        Assert.Equal(DarksteelOre, sale.ItemId);
        Assert.Equal(10u, sale.Quantity);
        Assert.Equal(200u, sale.UnitPrice);
        Assert.Equal(2_000, sale.GrossGil);
        Assert.Equal(1_900, sale.NetGil);
        Assert.Equal(currentRead, sale.SoldAtUtc);
        Assert.Equal(previousRead, sale.SoldAfterUtc);
        Assert.Equal(listedAt, sale.ListedAtUtc);
    }

    [Theory]
    [InlineData(11_900u, SnapshotDiffCorroboration.Corroborated)]
    [InlineData(10_500u, SnapshotDiffCorroboration.Partial)]
    [InlineData(10_000u, SnapshotDiffCorroboration.Uncorroborated)]
    public void InferredSaleCarriesWhatTheRetainerGilSaysAboutIt(uint currentGil, object expected)
    {
        var previous = Snapshot(Morning, 10_000, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(Morning.AddHours(1), currentGil);

        var sale = Assert.Single(InferredSales.From(SnapshotDiff.Compute(previous, current, []), CityTaxPercent));

        Assert.Equal((SnapshotDiffCorroboration)expected, sale.GilCorroboration);
        Assert.Equal(SaleConfidence.Inferred, sale.Confidence);
    }

    [Fact]
    public void ConfirmedSaleThatAbsorbsAnUncorroboratedDiffStaysConfirmed()
    {
        var previous = Snapshot(Morning, 10_000, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(Morning.AddHours(1), 10_000);
        var inferred = Assert.Single(InferredSales.From(SnapshotDiff.Compute(previous, current, []), CityTaxPercent));

        var merged = SaleMatcher.Merge(Of(inferred, History(Morning.AddMinutes(20), DarksteelOre, 10, 200)).AsSpan());

        var sale = Assert.Single(merged);
        Assert.Equal(SaleConfidence.Confirmed, sale.Confidence);
        Assert.Equal(SnapshotDiffCorroboration.NoInferredSales, sale.GilCorroboration);
    }

    [Fact]
    public void DiffWithoutSalesGivesNoRecords()
    {
        var previous = Snapshot(Morning, 0, Slot(0, DarksteelOre, 10, 200));
        var current = Snapshot(Morning.AddHours(1), 0, Slot(0, DarksteelOre, 10, 190));

        Assert.Empty(InferredSales.From(SnapshotDiff.Compute(previous, current, []), CityTaxPercent));
    }
}
