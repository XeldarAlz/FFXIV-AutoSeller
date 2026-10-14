using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

[Serializable]
public sealed class PricingChoices
{
    internal static readonly PricingRange<int> RepriceCooldownMinutesRange = new(
        (int)PricingSettings.RepriceCooldownRange.Minimum.TotalMinutes,
        (int)PricingSettings.RepriceCooldownRange.Default.TotalMinutes,
        (int)PricingSettings.RepriceCooldownRange.Maximum.TotalMinutes);

    internal static readonly PricingRange<int> PriceWarWindowHoursRange = new(
        (int)PricingSettings.PriceWarWindowRange.Minimum.TotalHours,
        (int)PricingSettings.PriceWarWindowRange.Default.TotalHours,
        (int)PricingSettings.PriceWarWindowRange.Maximum.TotalHours);

    public PricingMode Mode { get; set; } = PricingMode.Undercut;

    public UndercutUnit UndercutUnit { get; set; } = UndercutUnit.Gil;

    public uint UndercutGil { get; set; } = PricingSettings.UndercutGilRange.Default;

    public int UndercutBasisPoints { get; set; } = PricingSettings.UndercutBasisPointsRange.Default;

    public int MinimumReferenceRatioPercent { get; set; } = PricingSettings.MinimumReferenceRatioPercentRange.Default;

    public int BaitRatioPercent { get; set; } = PricingSettings.BaitRatioPercentRange.Default;

    public int StepLimitPercent { get; set; } = PricingSettings.StepLimitPercentRange.Default;

    public bool RaisePrices { get; set; } = true;

    public int RaiseGapPercent { get; set; } = PricingSettings.RaiseGapPercentRange.Default;

    public int MaximumRaisePercent { get; set; } = PricingSettings.MaximumRaisePercentRange.Default;

    public int RepriceCooldownMinutes { get; set; } = RepriceCooldownMinutesRange.Default;

    public int PriceWarDropCount { get; set; } = PricingSettings.PriceWarDropCountRange.Default;

    public int PriceWarWindowHours { get; set; } = PriceWarWindowHoursRange.Default;

    public BelowFloorNewLot BelowFloorNewLot { get; set; } = BelowFloorNewLot.KeepInBags;

    public BelowFloorListing BelowFloorListing { get; set; } = BelowFloorListing.KeepListed;

    internal PricingSettings Build() => new()
    {
        Mode = Mode,
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

    internal bool Repair()
    {
        var changed = SettingRepair.KnownValue(Mode, PricingMode.Undercut, out var mode);
        changed |= SettingRepair.KnownValue(UndercutUnit, UndercutUnit.Gil, out var undercutUnit);
        changed |= SettingRepair.KnownValue(BelowFloorNewLot, BelowFloorNewLot.KeepInBags, out var belowFloorNewLot);
        changed |= SettingRepair.KnownValue(BelowFloorListing, BelowFloorListing.KeepListed, out var belowFloorListing);
        changed |= Clamp(UndercutGil, PricingSettings.UndercutGilRange, out var undercutGil);
        changed |= Clamp(UndercutBasisPoints, PricingSettings.UndercutBasisPointsRange, out var undercutBasisPoints);
        changed |= Clamp(MinimumReferenceRatioPercent, PricingSettings.MinimumReferenceRatioPercentRange, out var minimumReferenceRatioPercent);
        changed |= Clamp(BaitRatioPercent, PricingSettings.BaitRatioPercentRange, out var baitRatioPercent);
        changed |= Clamp(StepLimitPercent, PricingSettings.StepLimitPercentRange, out var stepLimitPercent);
        changed |= Clamp(RaiseGapPercent, PricingSettings.RaiseGapPercentRange, out var raiseGapPercent);
        changed |= Clamp(MaximumRaisePercent, PricingSettings.MaximumRaisePercentRange, out var maximumRaisePercent);
        changed |= Clamp(RepriceCooldownMinutes, RepriceCooldownMinutesRange, out var repriceCooldownMinutes);
        changed |= Clamp(PriceWarDropCount, PricingSettings.PriceWarDropCountRange, out var priceWarDropCount);
        changed |= Clamp(PriceWarWindowHours, PriceWarWindowHoursRange, out var priceWarWindowHours);
        Mode = mode;
        UndercutUnit = undercutUnit;
        BelowFloorNewLot = belowFloorNewLot;
        BelowFloorListing = belowFloorListing;
        UndercutGil = undercutGil;
        UndercutBasisPoints = undercutBasisPoints;
        MinimumReferenceRatioPercent = minimumReferenceRatioPercent;
        BaitRatioPercent = baitRatioPercent;
        StepLimitPercent = stepLimitPercent;
        RaiseGapPercent = raiseGapPercent;
        MaximumRaisePercent = maximumRaisePercent;
        RepriceCooldownMinutes = repriceCooldownMinutes;
        PriceWarDropCount = priceWarDropCount;
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
