using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal static class PriceMath
{
    public const int BasisPointScale = 10_000;

    public static bool ExceedsStepLimit(uint current, uint target, int stepLimitPercent)
    {
        if (target >= current)
        {
            return false;
        }

        return Gil.ExceedsRatio(current - target, current, stepLimitPercent, Gil.PercentScale);
    }
}
