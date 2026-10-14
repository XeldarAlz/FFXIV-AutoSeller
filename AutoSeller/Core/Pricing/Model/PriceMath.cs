using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal static class PriceMath
{
    public const int BasisPointScale = 10_000;

    public static long ScaleDown(long amount, long numerator, long denominator)
    {
        return Gil.SaturatingMultiply(amount, numerator) / denominator;
    }

    public static long ScaleUp(long amount, long numerator, long denominator)
    {
        var product = Gil.SaturatingMultiply(amount, numerator);
        var quotient = product / denominator;
        return product % denominator > 0 ? Gil.SaturatingAdd(quotient, 1) : quotient;
    }

    public static bool ExceedsRatio(long amount, long baseline, long numerator, long denominator)
    {
        return Gil.SaturatingMultiply(amount, denominator) > Gil.SaturatingMultiply(baseline, numerator);
    }

    public static bool ExceedsStepLimit(uint current, uint target, int stepLimitPercent)
    {
        if (target >= current)
        {
            return false;
        }

        return ExceedsRatio(current - target, current, stepLimitPercent, Gil.PercentScale);
    }

    public static bool BelowRatio(long amount, long baseline, long numerator, long denominator)
    {
        return Gil.SaturatingMultiply(amount, denominator) < Gil.SaturatingMultiply(baseline, numerator);
    }
}
