using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Tasks.Model;

internal enum ListingDuty : byte
{
    Due,
    NotManaged,
    CoolingDown,
    Handled,
}

internal static class ListingDuties
{
    public static ListingDuty Of(RuleBook rules, in ListingSlot listing, byte itemUiCategory, uint referenceUnitPrice, in RepriceEntry history, TimeSpan cooldown, bool handled, DateTime nowUtc)
    {
        if (handled)
        {
            return ListingDuty.Handled;
        }

        if (!PricingRule.Manages(rules, listing.Key, itemUiCategory, referenceUnitPrice))
        {
            return ListingDuty.NotManaged;
        }

        return history.IsCoolingDown(nowUtc, cooldown) ? ListingDuty.CoolingDown : ListingDuty.Due;
    }
}
