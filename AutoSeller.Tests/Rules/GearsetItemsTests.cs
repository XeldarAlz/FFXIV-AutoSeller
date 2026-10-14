using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Shared;
using static AutoSeller.Tests.Rules.RuleFixtures;

namespace AutoSeller.Tests.Rules;

public sealed class GearsetItemsTests
{
    [Fact]
    public void RawIdBelowTheOffsetIsNormalQuality()
    {
        Assert.Equal(new ItemKey(Sword, false), GearsetItems.Decode(Sword));
    }

    [Fact]
    public void RawIdAboveTheOffsetIsHighQuality()
    {
        Assert.Equal(new ItemKey(Sword, true), GearsetItems.Decode(GearsetItems.HighQualityOffset + Sword));
    }

    [Fact]
    public void GuardMatchesItemAndQuality()
    {
        var items = GearsetItems.From([Sword, GearsetItems.HighQualityOffset + Ring]);

        Assert.True(items.Contains(Sword, false));
        Assert.False(items.Contains(Sword, true));
        Assert.True(items.Contains(Ring, true));
        Assert.False(items.Contains(Ring, false));
        Assert.False(items.Contains(Ore, false));
    }

    [Fact]
    public void EmptySlotsAndRepeatsAreDropped()
    {
        var items = GearsetItems.From([0, Sword, Sword, GearsetItems.HighQualityOffset, Ring]);

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void EmptyGuardMatchesNothing()
    {
        Assert.False(GearsetItems.Empty.Contains(Sword, false));
        Assert.False(default(GearsetItems).Contains(Sword, false));
        Assert.Equal(0, default(GearsetItems).Count);
    }
}
