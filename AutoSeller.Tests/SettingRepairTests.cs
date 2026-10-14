using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Tests;

public sealed class SettingRepairTests
{
    [Fact]
    public void KnownValueIsKept()
    {
        Assert.False(SettingRepair.KnownValue(PricingMode.Match, PricingMode.Undercut, out var repaired));

        Assert.Equal(PricingMode.Match, repaired);
    }

    [Fact]
    public void UnknownValueTakesTheFallback()
    {
        Assert.True(SettingRepair.KnownValue((PricingMode)200, PricingMode.Undercut, out var repaired));

        Assert.Equal(PricingMode.Undercut, repaired);
    }
}
