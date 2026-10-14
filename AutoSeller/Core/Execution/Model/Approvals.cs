using System.Collections.Immutable;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Retainers.Model;

namespace AutoSeller.Core.Execution.Model;

internal static class Approvals
{
    public static ImmutableArray<ApprovedPrice> Collect(
        PlanEdits edits,
        ReadOnlySpan<RetainerSnapshot> markets,
        ReadOnlySpan<ulong> authorizedRetainerIds,
        ReadOnlySpan<ApprovedPrice> playerApprovals)
    {
        var approved = ImmutableArray.CreateBuilder<ApprovedPrice>();
        for (var marketIndex = 0; marketIndex < markets.Length; marketIndex++)
        {
            var market = markets[marketIndex];
            if (!authorizedRetainerIds.Contains(market.RetainerId))
            {
                continue;
            }

            var listings = market.Slots;
            for (var slotIndex = 0; slotIndex < listings.Length; slotIndex++)
            {
                var listing = listings[slotIndex];
                if (edits.TryGetPrice(PlanRowKey.ForListing(market.RetainerId, listing), out var planPrice))
                {
                    approved.Add(ApprovedPrice.For(market.RetainerId, listing, planPrice));
                    continue;
                }

                if (TryFindLatest(playerApprovals, market.RetainerId, listing, out var playerApproval))
                {
                    approved.Add(playerApproval);
                }
            }
        }

        return approved.ToImmutable();
    }

    private static bool TryFindLatest(ReadOnlySpan<ApprovedPrice> playerApprovals, ulong retainerId, in ListingSlot listing, out ApprovedPrice latest)
    {
        for (var index = playerApprovals.Length - 1; index >= 0; index--)
        {
            if (playerApprovals[index].Covers(retainerId, listing.Slot, listing.Key, listing.UnitPrice))
            {
                latest = playerApprovals[index];
                return true;
            }
        }

        latest = default;
        return false;
    }
}
