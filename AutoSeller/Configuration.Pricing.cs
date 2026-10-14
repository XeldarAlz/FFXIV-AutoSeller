using AutoSeller.Core.Pricing.Model;

namespace AutoSeller;

public sealed partial class Configuration
{
    public static readonly int MinimumRepriceCooldownMinutes = (int)PricingSettings.RepriceCooldownRange.Minimum.TotalMinutes;
    public static readonly int MaximumRepriceCooldownMinutes = (int)PricingSettings.RepriceCooldownRange.Maximum.TotalMinutes;
    public static readonly int MinimumPriceWarWindowHours = (int)PricingSettings.PriceWarWindowRange.Minimum.TotalHours;
    public static readonly int MaximumPriceWarWindowHours = (int)PricingSettings.PriceWarWindowRange.Maximum.TotalHours;

    public PricingMode PricingMode { get; set; } = PricingMode.Undercut;

    public UndercutUnit UndercutUnit { get; set; } = UndercutUnit.Gil;

    public uint UndercutGil { get; set; } = PricingSettings.UndercutGilRange.Default;

    public int UndercutBasisPoints { get; set; } = PricingSettings.UndercutBasisPointsRange.Default;

    public int MinimumReferenceRatioPercent { get; set; } = PricingSettings.MinimumReferenceRatioPercentRange.Default;

    public int BaitRatioPercent { get; set; } = PricingSettings.BaitRatioPercentRange.Default;

    public int StepLimitPercent { get; set; } = PricingSettings.StepLimitPercentRange.Default;

    public bool RaisePrices { get; set; } = true;

    public int RaiseGapPercent { get; set; } = PricingSettings.RaiseGapPercentRange.Default;

    public int MaximumRaisePercent { get; set; } = PricingSettings.MaximumRaisePercentRange.Default;

    public int RepriceCooldownMinutes { get; set; } = (int)PricingSettings.RepriceCooldownRange.Default.TotalMinutes;

    public int PriceWarDropCount { get; set; } = PricingSettings.PriceWarDropCountRange.Default;

    public int PriceWarWindowHours { get; set; } = (int)PricingSettings.PriceWarWindowRange.Default.TotalHours;

    public BelowFloorNewLot BelowFloorNewLot { get; set; } = BelowFloorNewLot.KeepInBags;

    public BelowFloorListing BelowFloorListing { get; set; } = BelowFloorListing.KeepListed;

    internal PricingSettings BuildPricingSettings() => new()
    {
        Mode = PricingMode,
        UndercutUnit = UndercutUnit,
        UndercutGil = UndercutGil,
        UndercutBasisPoints = UndercutBasisPoints,
        MinimumReferenceRatioPercent = MinimumReferenceRatioPercent,
        BaitRatioPercent = BaitRatioPercent,
        StepLimitPercent = StepLimitPercent,
        RaisePrices = RaisePrices,
        RaiseGapPercent = RaiseGapPercent,
        MaximumRaisePercent = MaximumRaisePercent,
        RepriceCooldown = TimeSpan.FromMinutes(RepriceCooldownMinutes),
        PriceWarDropCount = PriceWarDropCount,
        PriceWarWindow = TimeSpan.FromHours(PriceWarWindowHours),
        BelowFloorNewLot = BelowFloorNewLot,
        BelowFloorListing = BelowFloorListing,
    };

    private bool RepairPricingSettings()
    {
        var changed = false;
        if (!Enum.IsDefined(PricingMode))
        {
            PricingMode = PricingMode.Undercut;
            changed = true;
        }

        if (!Enum.IsDefined(UndercutUnit))
        {
            UndercutUnit = UndercutUnit.Gil;
            changed = true;
        }

        if (!Enum.IsDefined(BelowFloorNewLot))
        {
            BelowFloorNewLot = BelowFloorNewLot.KeepInBags;
            changed = true;
        }

        if (!Enum.IsDefined(BelowFloorListing))
        {
            BelowFloorListing = BelowFloorListing.KeepListed;
            changed = true;
        }

        changed |= Clamp(UndercutGil, PricingSettings.UndercutGilRange, out var undercutGil);
        changed |= Clamp(UndercutBasisPoints, PricingSettings.UndercutBasisPointsRange, out var undercutBasisPoints);
        changed |= Clamp(MinimumReferenceRatioPercent, PricingSettings.MinimumReferenceRatioPercentRange, out var minimumReferenceRatioPercent);
        changed |= Clamp(BaitRatioPercent, PricingSettings.BaitRatioPercentRange, out var baitRatioPercent);
        changed |= Clamp(StepLimitPercent, PricingSettings.StepLimitPercentRange, out var stepLimitPercent);
        changed |= Clamp(RaiseGapPercent, PricingSettings.RaiseGapPercentRange, out var raiseGapPercent);
        changed |= Clamp(MaximumRaisePercent, PricingSettings.MaximumRaisePercentRange, out var maximumRaisePercent);
        changed |= Clamp(PriceWarDropCount, PricingSettings.PriceWarDropCountRange, out var priceWarDropCount);
        UndercutGil = undercutGil;
        UndercutBasisPoints = undercutBasisPoints;
        MinimumReferenceRatioPercent = minimumReferenceRatioPercent;
        BaitRatioPercent = baitRatioPercent;
        StepLimitPercent = stepLimitPercent;
        RaiseGapPercent = raiseGapPercent;
        MaximumRaisePercent = maximumRaisePercent;
        PriceWarDropCount = priceWarDropCount;

        var cooldownMinutes = Math.Clamp(RepriceCooldownMinutes, MinimumRepriceCooldownMinutes, MaximumRepriceCooldownMinutes);
        var priceWarWindowHours = Math.Clamp(PriceWarWindowHours, MinimumPriceWarWindowHours, MaximumPriceWarWindowHours);
        changed |= cooldownMinutes != RepriceCooldownMinutes || priceWarWindowHours != PriceWarWindowHours;
        RepriceCooldownMinutes = cooldownMinutes;
        PriceWarWindowHours = priceWarWindowHours;
        return changed;
    }

    private static bool Clamp<T>(T value, in PricingRange<T> range, out T clamped)
        where T : struct, IComparable<T>
    {
        clamped = range.Clamp(value);
        return clamped.CompareTo(value) != 0;
    }
}
