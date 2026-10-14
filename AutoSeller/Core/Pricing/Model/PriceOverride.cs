using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal enum PriceOverrideKind : byte
{
    None,
    Approved,
    Player,
}

internal readonly record struct PriceOverride
{
    private PriceOverride(PriceOverrideKind kind, uint price)
    {
        Kind = kind;
        Price = price;
    }

    public static PriceOverride None => default;

    public PriceOverrideKind Kind { get; }

    public uint Price { get; }

    public static PriceOverride ApprovedAt(uint price)
    {
        return new PriceOverride(PriceOverrideKind.Approved, MarketLimits.ClampUnitPrice(price));
    }

    public static PriceOverride SetByPlayer(uint price)
    {
        return new PriceOverride(PriceOverrideKind.Player, MarketLimits.ClampUnitPrice(price));
    }
}
