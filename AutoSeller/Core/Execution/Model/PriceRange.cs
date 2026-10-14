namespace AutoSeller.Core.Execution.Model;

internal static class PriceRange
{
    public const uint Minimum = 1;
    public const uint Maximum = 999_999_999;

    public static bool Contains(uint price)
    {
        return price is >= Minimum and <= Maximum;
    }
}
