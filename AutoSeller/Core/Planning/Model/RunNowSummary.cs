using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.Planning.Model;

internal readonly record struct RunNowSummary(int Undercuts, int Checks, int ItemsToSell, bool ReadsFirst)
{
    public bool HasWork => Checks > 0 || ItemsToSell > 0 || ReadsFirst;

    public static RunNowSummary For(SellPlan? plan, bool undercutAllListings) => For(plan, undercutAllListings, RunWork.All);

    public static RunNowSummary For(SellPlan? plan, bool undercutAllListings, RunWork work)
    {
        if (plan is null)
        {
            return default;
        }

        var all = Of(plan.Rows.AsSpan());
        var reprices = (work & RunWork.Reprice) != 0;
        var sells = (work & RunWork.Sell) != 0;
        return new RunNowSummary(
            reprices ? all.Undercuts : 0,
            reprices ? all.Checks : 0,
            sells ? all.ItemsToSell : 0,
            reprices && !plan.HasMarkets && undercutAllListings && !plan.Retainers.IsEmpty);
    }

    public static RunNowSummary Of(ReadOnlySpan<PlanRow> rows)
    {
        var undercuts = 0;
        var checks = 0;
        var itemsToSell = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (row.Has(PlanRowEdits.Excluded))
            {
                continue;
            }

            if (row.IsListing)
            {
                checks += row.Searches ? 1 : 0;
                undercuts += row.Searches && row.Writes ? 1 : 0;
                continue;
            }

            if (Sells(row) && !SoldEarlier(rows, index, row.Key.ItemId))
            {
                itemsToSell++;
            }
        }

        return new RunNowSummary(undercuts, checks, itemsToSell, false);
    }

    private static bool Sells(in PlanRow row) => row.Kind switch
    {
        RunRowKind.Lot => row.IsPlaced,
        RunRowKind.Vendor => row.Decision.Action == SellAction.Vendor,
        _ => false,
    };

    private static bool SoldEarlier(ReadOnlySpan<PlanRow> rows, int end, uint itemId)
    {
        for (var index = 0; index < end; index++)
        {
            var row = rows[index];
            if (row.Key.ItemId == itemId && !row.Has(PlanRowEdits.Excluded) && Sells(row))
            {
                return true;
            }
        }

        return false;
    }
}
