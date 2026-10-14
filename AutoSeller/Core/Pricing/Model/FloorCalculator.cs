using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal static class FloorCalculator
{
    public static uint Calculate(uint ruleFloor, uint vendorPrice, in ReferencePrice reference, int minimumReferenceRatioPercent)
    {
        var floor = Math.Max(ruleFloor, Gil.SaturatingAdd(vendorPrice, 1));
        if (reference.Exists)
        {
            var referenceFloor = PriceMath.ScaleUp(reference.AverageSalePrice, minimumReferenceRatioPercent, PriceMath.PercentScale);
            floor = Math.Max(floor, referenceFloor);
        }

        return MarketLimits.ClampUnitPrice(floor);
    }
}
