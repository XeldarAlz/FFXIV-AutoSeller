using AutoSeller.Core.Pricing.Model;

namespace AutoSeller.Tests.Pricing;

public sealed class AltSellersTests
{
    private const ulong Current = 0x0040_0000_0000_0001;
    private const ulong FirstAlt = 0x0040_0000_0000_0002;
    private const ulong SecondAlt = 0x0040_0000_0000_0003;
    private const ulong CurrentRetainer = 0x0030_0000_0000_0001;
    private const ulong FirstAltRetainer = 0x0030_0000_0000_0002;
    private const ulong SecondAltRetainer = 0x0030_0000_0000_0003;

    [Fact]
    public void EveryOtherCharacterAndItsRetainersAreAlts()
    {
        var alts = AltSellers.Of(Current, [Alt(Current, CurrentRetainer), Alt(SecondAlt, SecondAltRetainer), Alt(FirstAlt, FirstAltRetainer)]);

        Assert.Equal([FirstAlt, SecondAlt], alts.ContentIds.AsSpan().ToArray());
        Assert.Equal([FirstAltRetainer, SecondAltRetainer], alts.RetainerIds.AsSpan().ToArray());
    }

    [Fact]
    public void ZeroAndRepeatedIdsAreDropped()
    {
        var alts = AltSellers.Of(Current, [Alt(FirstAlt, FirstAltRetainer, 0, FirstAltRetainer), Alt(0), Alt(FirstAlt, FirstAltRetainer)]);

        Assert.Equal([FirstAlt], alts.ContentIds.AsSpan().ToArray());
        Assert.Equal([FirstAltRetainer], alts.RetainerIds.AsSpan().ToArray());
    }

    [Fact]
    public void ACharacterWithoutRetainerDataStillCountsAsAnAlt()
    {
        var alts = AltSellers.Of(Current, [new KnownAlt(FirstAlt, default)]);

        Assert.Equal([FirstAlt], alts.ContentIds.AsSpan().ToArray());
        Assert.True(alts.RetainerIds.IsEmpty);
    }

    [Fact]
    public void OnlyTheCurrentCharacterMeansNoAlts()
    {
        var alts = AltSellers.Of(Current, [Alt(Current, CurrentRetainer)]);

        Assert.True(alts.SameAs(AltSellers.None));
    }

    [Fact]
    public void AltSetsCompareByTheirIds()
    {
        var first = AltSellers.Of(Current, [Alt(FirstAlt, FirstAltRetainer)]);
        var again = AltSellers.Of(Current, [Alt(FirstAlt, FirstAltRetainer)]);
        var more = AltSellers.Of(Current, [Alt(FirstAlt, FirstAltRetainer), Alt(SecondAlt)]);

        Assert.True(first.SameAs(again));
        Assert.False(first.SameAs(more));
        Assert.False(first.SameAs(AltSellers.None));
    }

    [Fact]
    public void AltsBecomeOwnSellers()
    {
        var alts = AltSellers.Of(Current, [Alt(FirstAlt, FirstAltRetainer)]);
        var sellers = OwnSellers.Of([CurrentRetainer], Current, alts);

        Assert.True(sellers.IsOwnRetainer(FirstAltRetainer));
        Assert.True(sellers.IsOwnCharacter(FirstAlt));
        Assert.True(sellers.IsOwnRetainer(CurrentRetainer));
    }

    private static KnownAlt Alt(ulong contentId, params ulong[] retainerIds)
    {
        return new KnownAlt(contentId, [.. retainerIds]);
    }
}
