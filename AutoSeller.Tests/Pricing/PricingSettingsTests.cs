using AutoSeller.Core.Pricing.Model;

namespace AutoSeller.Tests.Pricing;

public sealed class PricingSettingsTests
{
    [Fact]
    public void DefaultsMatchThePricingTable()
    {
        var settings = new PricingSettings();

        Assert.Equal(PricingMode.Undercut, settings.Mode);
        Assert.Equal(UndercutUnit.Gil, settings.UndercutUnit);
        Assert.Equal(1u, settings.UndercutGil);
        Assert.Equal(50, settings.MinimumReferenceRatioPercent);
        Assert.Equal(40, settings.BaitRatioPercent);
        Assert.Equal(25, settings.StepLimitPercent);
        Assert.True(settings.RaisePrices);
        Assert.Equal(10, settings.RaiseGapPercent);
        Assert.Equal(50, settings.MaximumRaisePercent);
        Assert.Equal(TimeSpan.FromHours(2), settings.RepriceCooldown);
        Assert.Equal(3, settings.PriceWarDropCount);
        Assert.Equal(TimeSpan.FromHours(24), settings.PriceWarWindow);
        Assert.Equal(BelowFloorNewLot.KeepInBags, settings.BelowFloorNewLot);
        Assert.Equal(BelowFloorListing.KeepListed, settings.BelowFloorListing);
        Assert.Equal(TimeSpan.FromDays(3), PricingSettings.BelowFloorReturnDelay);
    }

    [Fact]
    public void RangesMatchThePricingTable()
    {
        Assert.Equal((1u, 10_000u), (PricingSettings.UndercutGilRange.Minimum, PricingSettings.UndercutGilRange.Maximum));
        Assert.Equal((10, 1_000), (PricingSettings.UndercutBasisPointsRange.Minimum, PricingSettings.UndercutBasisPointsRange.Maximum));
        Assert.Equal((10, 100), (PricingSettings.MinimumReferenceRatioPercentRange.Minimum, PricingSettings.MinimumReferenceRatioPercentRange.Maximum));
        Assert.Equal((10, 90), (PricingSettings.BaitRatioPercentRange.Minimum, PricingSettings.BaitRatioPercentRange.Maximum));
        Assert.Equal((5, 90), (PricingSettings.StepLimitPercentRange.Minimum, PricingSettings.StepLimitPercentRange.Maximum));
        Assert.Equal(TimeSpan.FromMinutes(30), PricingSettings.RepriceCooldownRange.Minimum);
    }

    [Fact]
    public void ValuesBelowTheirRangeRiseToTheMinimum()
    {
        var settings = new PricingSettings
        {
            UndercutGil = 0,
            UndercutBasisPoints = 1,
            MinimumReferenceRatioPercent = 0,
            BaitRatioPercent = -5,
            StepLimitPercent = 1,
            RaiseGapPercent = 0,
            MaximumRaisePercent = 0,
            RepriceCooldown = TimeSpan.FromMinutes(5),
            PriceWarDropCount = 0,
            PriceWarWindow = TimeSpan.Zero,
        };

        Assert.Equal(1u, settings.UndercutGil);
        Assert.Equal(10, settings.UndercutBasisPoints);
        Assert.Equal(10, settings.MinimumReferenceRatioPercent);
        Assert.Equal(10, settings.BaitRatioPercent);
        Assert.Equal(5, settings.StepLimitPercent);
        Assert.Equal(PricingSettings.RaiseGapPercentRange.Minimum, settings.RaiseGapPercent);
        Assert.Equal(PricingSettings.MaximumRaisePercentRange.Minimum, settings.MaximumRaisePercent);
        Assert.Equal(TimeSpan.FromMinutes(30), settings.RepriceCooldown);
        Assert.Equal(PricingSettings.PriceWarDropCountRange.Minimum, settings.PriceWarDropCount);
        Assert.Equal(PricingSettings.PriceWarWindowRange.Minimum, settings.PriceWarWindow);
    }

    [Fact]
    public void ValuesAboveTheirRangeFallToTheMaximum()
    {
        var settings = new PricingSettings() with
        {
            UndercutGil = 50_000,
            UndercutBasisPoints = 5_000,
            MinimumReferenceRatioPercent = 150,
            BaitRatioPercent = 95,
            StepLimitPercent = 100,
            RepriceCooldown = TimeSpan.FromDays(30),
        };

        Assert.Equal(10_000u, settings.UndercutGil);
        Assert.Equal(1_000, settings.UndercutBasisPoints);
        Assert.Equal(100, settings.MinimumReferenceRatioPercent);
        Assert.Equal(90, settings.BaitRatioPercent);
        Assert.Equal(90, settings.StepLimitPercent);
        Assert.Equal(PricingSettings.RepriceCooldownRange.Maximum, settings.RepriceCooldown);
    }
}
