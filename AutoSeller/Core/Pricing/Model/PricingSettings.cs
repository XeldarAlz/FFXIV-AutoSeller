using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Pricing.Model;

internal enum PricingMode : byte
{
    Undercut,
    Match,
}

internal enum UndercutUnit : byte
{
    Gil,
    Percent,
}

internal enum BelowFloorNewLot : byte
{
    KeepInBags,
    ListAtFloor,
}

internal enum BelowFloorListing : byte
{
    KeepListed,
    ReturnAfterDelay,
}

internal sealed record PricingSettings
{
    public static readonly PricingRange<uint> UndercutGilRange = new(1, 1, 10_000);
    public static readonly PricingRange<int> UndercutBasisPointsRange = new(10, 100, 1_000);
    public static readonly PricingRange<int> MinimumReferenceRatioPercentRange = new(10, 50, 100);
    public static readonly PricingRange<int> BaitRatioPercentRange = new(10, 40, 90);
    public static readonly PricingRange<int> StepLimitPercentRange = new(5, 25, 90);
    public static readonly PricingRange<int> RaiseGapPercentRange = new(1, 10, 100);
    public static readonly PricingRange<int> MaximumRaisePercentRange = new(5, 50, 100);
    public static readonly PricingRange<int> PriceWarDropCountRange = new(2, 3, 10);
    public static readonly PricingRange<TimeSpan> RepriceCooldownRange = new(TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromDays(7));
    public static readonly PricingRange<TimeSpan> PriceWarWindowRange = new(TimeSpan.FromHours(1), TimeSpan.FromHours(24), TimeSpan.FromDays(7));
    public static readonly PricingRange<byte> ListingsToKeepUpRange = new(1, ItemRule.DefaultListingsToKeepUp, ItemRule.MaximumListingsToKeepUp);
    public static readonly TimeSpan BelowFloorReturnDelay = TimeSpan.FromDays(3);

    private readonly uint undercutGil = UndercutGilRange.Default;
    private readonly int undercutBasisPoints = UndercutBasisPointsRange.Default;
    private readonly int minimumReferenceRatioPercent = MinimumReferenceRatioPercentRange.Default;
    private readonly int baitRatioPercent = BaitRatioPercentRange.Default;
    private readonly int stepLimitPercent = StepLimitPercentRange.Default;
    private readonly int raiseGapPercent = RaiseGapPercentRange.Default;
    private readonly int maximumRaisePercent = MaximumRaisePercentRange.Default;
    private readonly int priceWarDropCount = PriceWarDropCountRange.Default;
    private readonly TimeSpan repriceCooldown = RepriceCooldownRange.Default;
    private readonly TimeSpan priceWarWindow = PriceWarWindowRange.Default;

    public PricingMode Mode { get; init; } = PricingMode.Undercut;

    public UndercutUnit UndercutUnit { get; init; } = UndercutUnit.Gil;

    public uint UndercutGil
    {
        get => undercutGil;
        init => undercutGil = UndercutGilRange.Clamp(value);
    }

    public int UndercutBasisPoints
    {
        get => undercutBasisPoints;
        init => undercutBasisPoints = UndercutBasisPointsRange.Clamp(value);
    }

    public int MinimumReferenceRatioPercent
    {
        get => minimumReferenceRatioPercent;
        init => minimumReferenceRatioPercent = MinimumReferenceRatioPercentRange.Clamp(value);
    }

    public int BaitRatioPercent
    {
        get => baitRatioPercent;
        init => baitRatioPercent = BaitRatioPercentRange.Clamp(value);
    }

    public int StepLimitPercent
    {
        get => stepLimitPercent;
        init => stepLimitPercent = StepLimitPercentRange.Clamp(value);
    }

    public bool RaisePrices { get; init; } = true;

    public int RaiseGapPercent
    {
        get => raiseGapPercent;
        init => raiseGapPercent = RaiseGapPercentRange.Clamp(value);
    }

    public int MaximumRaisePercent
    {
        get => maximumRaisePercent;
        init => maximumRaisePercent = MaximumRaisePercentRange.Clamp(value);
    }

    public TimeSpan RepriceCooldown
    {
        get => repriceCooldown;
        init => repriceCooldown = RepriceCooldownRange.Clamp(value);
    }

    public int PriceWarDropCount
    {
        get => priceWarDropCount;
        init => priceWarDropCount = PriceWarDropCountRange.Clamp(value);
    }

    public TimeSpan PriceWarWindow
    {
        get => priceWarWindow;
        init => priceWarWindow = PriceWarWindowRange.Clamp(value);
    }

    public BelowFloorNewLot BelowFloorNewLot { get; init; } = BelowFloorNewLot.KeepInBags;

    public BelowFloorListing BelowFloorListing { get; init; } = BelowFloorListing.KeepListed;
}
