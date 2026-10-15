using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Inventory.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Tests.Inventory;

public sealed class StockCoverageTests
{
    private const ulong Alpha = 11;
    private const ulong Beta = 12;
    private const uint Bag = 1;
    private const uint Saddlebag = 4_000;
    private const uint Page = 10_003;

    [Fact]
    public void AReadContainerIsCoveredAndAnUnreadOneIsNot()
    {
        var coverage = new StockCoverage([Saddlebag], 0);

        Assert.True(coverage.Covers(Bag, 0));
        Assert.False(coverage.Covers(Saddlebag, 0));
    }

    [Fact]
    public void RetainerPagesAreCoveredOnlyForTheRetainerWhosePagesWereRead()
    {
        var coverage = new StockCoverage([], Alpha);

        Assert.True(coverage.Covers(Page, Alpha));
        Assert.False(coverage.Covers(Page, Beta));
        Assert.False(coverage.Covers(SourceContainers.RetainerCrystals, Beta));
        Assert.False(default(StockCoverage).Covers(Page, 0));
    }

    [Fact]
    public void AListingRowIsAlwaysCovered()
    {
        var listing = PlanRowKey.ForListing(Alpha, new ListingSlot(2, 5_000, false, 5, 100));

        Assert.True(StockCoverage.NothingRead.Covers(listing));
    }

    [Fact]
    public void NothingReadCoversNoContainerOfTheCharacter()
    {
        var coverage = StockCoverage.NothingRead;

        Assert.False(coverage.Covers(Bag, 0));
        Assert.False(coverage.Covers(SourceContainers.Crystals, 0));
        Assert.False(coverage.Covers(Saddlebag, 0));
        Assert.False(coverage.Covers(3_200, 0));
        Assert.False(coverage.Covers(new SlotFacts(Alpha, 5_000, 1, Page, 0, 0, SlotSource.RetainerPage, false, false, false)));
    }
}
