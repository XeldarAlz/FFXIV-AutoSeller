using AutoSeller.Core.Pricing.Model;

namespace AutoSeller.Tests.Pricing;

public sealed class PricingChoicesTests
{
    [Fact]
    public void DefaultChoicesBuildTheDefaultSettings()
    {
        Assert.Equal(new PricingSettings(), new PricingChoices().Build());
    }

    [Fact]
    public void EveryChoiceReachesItsOwnSetting()
    {
        var choices = new PricingChoices
        {
            Mode = PricingMode.Match,
            UndercutUnit = UndercutUnit.Percent,
            UndercutGil = 7,
            UndercutBasisPoints = 250,
            MinimumReferenceRatioPercent = 60,
            BaitRatioPercent = 35,
            StepLimitPercent = 30,
            RaisePrices = false,
            RaiseGapPercent = 15,
            MaximumRaisePercent = 45,
            RepriceCooldownMinutes = 90,
            PriceWarDropCount = 4,
            PriceWarWindowHours = 12,
            BelowFloorNewLot = BelowFloorNewLot.ListAtFloor,
            BelowFloorListing = BelowFloorListing.ReturnAfterDelay,
        };

        var settings = choices.Build();

        Assert.Equal(PricingMode.Match, settings.Mode);
        Assert.Equal(UndercutUnit.Percent, settings.UndercutUnit);
        Assert.Equal(7u, settings.UndercutGil);
        Assert.Equal(250, settings.UndercutBasisPoints);
        Assert.Equal(60, settings.MinimumReferenceRatioPercent);
        Assert.Equal(35, settings.BaitRatioPercent);
        Assert.Equal(30, settings.StepLimitPercent);
        Assert.False(settings.RaisePrices);
        Assert.Equal(15, settings.RaiseGapPercent);
        Assert.Equal(45, settings.MaximumRaisePercent);
        Assert.Equal(TimeSpan.FromMinutes(90), settings.RepriceCooldown);
        Assert.Equal(4, settings.PriceWarDropCount);
        Assert.Equal(TimeSpan.FromHours(12), settings.PriceWarWindow);
        Assert.Equal(BelowFloorNewLot.ListAtFloor, settings.BelowFloorNewLot);
        Assert.Equal(BelowFloorListing.ReturnAfterDelay, settings.BelowFloorListing);
    }

    [Fact]
    public void ChoicesInRangeNeedNoRepair()
    {
        var choices = new PricingChoices { UndercutGil = 50, RepriceCooldownMinutes = 120, PriceWarWindowHours = 6 };

        Assert.False(choices.Repair());

        Assert.Equal(50u, choices.UndercutGil);
        Assert.Equal(120, choices.RepriceCooldownMinutes);
        Assert.Equal(6, choices.PriceWarWindowHours);
    }

    [Fact]
    public void UnknownModesFallBackToTheirDefaults()
    {
        var choices = new PricingChoices
        {
            Mode = (PricingMode)9,
            UndercutUnit = (UndercutUnit)9,
            BelowFloorNewLot = (BelowFloorNewLot)9,
            BelowFloorListing = (BelowFloorListing)9,
        };

        Assert.True(choices.Repair());

        Assert.Equal(PricingMode.Undercut, choices.Mode);
        Assert.Equal(UndercutUnit.Gil, choices.UndercutUnit);
        Assert.Equal(BelowFloorNewLot.KeepInBags, choices.BelowFloorNewLot);
        Assert.Equal(BelowFloorListing.KeepListed, choices.BelowFloorListing);
    }

    [Fact]
    public void ValuesBelowTheirRangeRiseToTheMinimum()
    {
        var choices = new PricingChoices
        {
            UndercutGil = 0,
            UndercutBasisPoints = 0,
            MinimumReferenceRatioPercent = 0,
            BaitRatioPercent = -1,
            StepLimitPercent = 0,
            RaiseGapPercent = 0,
            MaximumRaisePercent = 0,
            RepriceCooldownMinutes = 0,
            PriceWarDropCount = 0,
            PriceWarWindowHours = 0,
        };

        Assert.True(choices.Repair());

        Assert.Equal(PricingSettings.UndercutGilRange.Minimum, choices.UndercutGil);
        Assert.Equal(PricingSettings.UndercutBasisPointsRange.Minimum, choices.UndercutBasisPoints);
        Assert.Equal(PricingSettings.MinimumReferenceRatioPercentRange.Minimum, choices.MinimumReferenceRatioPercent);
        Assert.Equal(PricingSettings.BaitRatioPercentRange.Minimum, choices.BaitRatioPercent);
        Assert.Equal(PricingSettings.StepLimitPercentRange.Minimum, choices.StepLimitPercent);
        Assert.Equal(PricingSettings.RaiseGapPercentRange.Minimum, choices.RaiseGapPercent);
        Assert.Equal(PricingSettings.MaximumRaisePercentRange.Minimum, choices.MaximumRaisePercent);
        Assert.Equal(30, choices.RepriceCooldownMinutes);
        Assert.Equal(PricingSettings.PriceWarDropCountRange.Minimum, choices.PriceWarDropCount);
        Assert.Equal(1, choices.PriceWarWindowHours);
    }

    [Fact]
    public void ValuesAboveTheirRangeFallToTheMaximum()
    {
        var choices = new PricingChoices
        {
            UndercutGil = uint.MaxValue,
            UndercutBasisPoints = int.MaxValue,
            MinimumReferenceRatioPercent = 500,
            BaitRatioPercent = 500,
            StepLimitPercent = 500,
            RaiseGapPercent = 500,
            MaximumRaisePercent = 500,
            RepriceCooldownMinutes = int.MaxValue,
            PriceWarDropCount = 500,
            PriceWarWindowHours = int.MaxValue,
        };

        Assert.True(choices.Repair());

        Assert.Equal(PricingSettings.UndercutGilRange.Maximum, choices.UndercutGil);
        Assert.Equal(PricingSettings.UndercutBasisPointsRange.Maximum, choices.UndercutBasisPoints);
        Assert.Equal(PricingSettings.MinimumReferenceRatioPercentRange.Maximum, choices.MinimumReferenceRatioPercent);
        Assert.Equal(PricingSettings.BaitRatioPercentRange.Maximum, choices.BaitRatioPercent);
        Assert.Equal(PricingSettings.StepLimitPercentRange.Maximum, choices.StepLimitPercent);
        Assert.Equal(PricingSettings.RaiseGapPercentRange.Maximum, choices.RaiseGapPercent);
        Assert.Equal(PricingSettings.MaximumRaisePercentRange.Maximum, choices.MaximumRaisePercent);
        Assert.Equal(7 * 24 * 60, choices.RepriceCooldownMinutes);
        Assert.Equal(PricingSettings.PriceWarDropCountRange.Maximum, choices.PriceWarDropCount);
        Assert.Equal(7 * 24, choices.PriceWarWindowHours);
    }

    [Fact]
    public void CooldownAndPriceWarRangesFollowThePricingSettings()
    {
        Assert.Equal(PricingSettings.RepriceCooldownRange.Default, TimeSpan.FromMinutes(PricingChoices.RepriceCooldownMinutesRange.Default));
        Assert.Equal(PricingSettings.RepriceCooldownRange.Maximum, TimeSpan.FromMinutes(PricingChoices.RepriceCooldownMinutesRange.Maximum));
        Assert.Equal(PricingSettings.PriceWarWindowRange.Default, TimeSpan.FromHours(PricingChoices.PriceWarWindowHoursRange.Default));
        Assert.Equal(PricingSettings.PriceWarWindowRange.Minimum, TimeSpan.FromHours(PricingChoices.PriceWarWindowHoursRange.Minimum));
    }
}
