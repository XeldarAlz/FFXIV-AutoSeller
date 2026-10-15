using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal readonly record struct VendorSettings(bool Enabled, ItemIdSet VendorList, uint JunkThreshold)
{
    public const uint DefaultJunkThreshold = 1_000;

    public bool VendorsAnything => Enabled || !VendorList.Ids.IsEmpty;

    public static VendorSettings Off => new(false, ItemIdSet.Empty, DefaultJunkThreshold);
}

internal enum VendorReason : byte
{
    VendorList,
    Junk,
    VendoringOff,
    NotInBags,
    ProtectedByAutoRetainer,
    Gear,
    HighQuality,
    Collectable,
    NeverSell,
    OnSellList,
    MatchedCategoryRule,
    NoVendorPrice,
    NoReferenceValue,
    NotJunk,
    RunCapReached,
    Excluded,
}

internal readonly record struct VendorVerdict(SlotFacts Slot, VendorReason Reason, long VendorValue)
{
    public bool IsVendored => Reason is VendorReason.VendorList or VendorReason.Junk;
}

internal static class VendorRules
{
    public static ImmutableArray<VendorVerdict> Plan(ImmutableArray<StockSlot> stock, RuleBook rules, long vendoredValueCap, PlanEdits edits)
    {
        var verdicts = new VendorVerdict[stock.Length];
        long vendoredValue = 0;
        for (var index = 0; index < stock.Length; index++)
        {
            var entry = stock[index];
            var verdict = new VendorVerdict(entry.Slot, Check(entry, rules), VendorValue(entry));
            if (verdict.IsVendored && edits.IsExcluded(PlanRowKey.ForVendor(entry.Slot)))
            {
                verdict = verdict with { Reason = VendorReason.Excluded };
            }

            if (verdict.IsVendored)
            {
                var total = Gil.SaturatingAdd(vendoredValue, verdict.VendorValue);
                if (total > vendoredValueCap)
                {
                    verdict = verdict with { Reason = VendorReason.RunCapReached };
                }
                else
                {
                    vendoredValue = total;
                }
            }

            verdicts[index] = verdict;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(verdicts);
    }

    public static ImmutableArray<VendorVerdict> Due(ImmutableArray<StockSlot> stock, RuleBook rules, long remainingValueCap, IReadOnlySet<PlanRowKey> handled, PlanEdits edits)
    {
        var open = ImmutableArray.CreateBuilder<StockSlot>(stock.Length);
        for (var index = 0; index < stock.Length; index++)
        {
            if (!handled.Contains(PlanRowKey.ForVendor(stock[index].Slot)))
            {
                open.Add(stock[index]);
            }
        }

        var verdicts = Plan(open.ToImmutable(), rules, Math.Max(0, remainingValueCap), edits);
        var due = ImmutableArray.CreateBuilder<VendorVerdict>();
        for (var index = 0; index < verdicts.Length; index++)
        {
            if (verdicts[index].IsVendored)
            {
                due.Add(verdicts[index]);
            }
        }

        return due.ToImmutable();
    }

    public static Decision DecisionFor(in VendorVerdict verdict, uint vendorPrice)
    {
        var reason = verdict.Reason == VendorReason.VendorList ? DecisionReason.VendorList : DecisionReason.Junk;
        return new Decision(SellAction.Vendor, vendorPrice, 0, reason, default);
    }

    public static VendorReason Check(in StockSlot stock, RuleBook rules)
    {
        if (Refusal(stock, rules) is { } refusal)
        {
            return refusal;
        }

        var settings = rules.Vendor;
        if (settings.VendorList.Contains(stock.Slot.ItemId))
        {
            return VendorReason.VendorList;
        }

        if (!settings.Enabled)
        {
            return VendorReason.VendoringOff;
        }

        if (stock.ReferenceUnitPrice == 0)
        {
            return VendorReason.NoReferenceValue;
        }

        var marketValue = Gil.SaturatingMultiply(stock.ReferenceUnitPrice, stock.Slot.Quantity);
        return marketValue < settings.JunkThreshold ? VendorReason.Junk : VendorReason.NotJunk;
    }

    public static long VendorValue(in StockSlot stock)
    {
        return Gil.SaturatingMultiply(stock.Item.VendorPrice, stock.Slot.Quantity);
    }

    public static bool CanBeVendored(in StockSlot stock, RuleBook rules)
    {
        return HoldingRefusal(stock, rules) is null && stock.Item.VendorPrice > 0;
    }

    private static VendorReason? Refusal(in StockSlot stock, RuleBook rules)
    {
        if (HoldingRefusal(stock, rules) is { } holding)
        {
            return holding;
        }

        var item = stock.Item;
        if (rules.IsOnSellList(stock.Slot.ItemId))
        {
            return VendorReason.OnSellList;
        }

        if (Eligibility.MatchCategory(rules, item.ItemUiCategory, stock.ReferenceUnitPrice, out _) == EligibilityReason.Eligible)
        {
            return VendorReason.MatchedCategoryRule;
        }

        return item.VendorPrice == 0 ? VendorReason.NoVendorPrice : null;
    }

    private static VendorReason? HoldingRefusal(in StockSlot stock, RuleBook rules)
    {
        var slot = stock.Slot;
        var item = stock.Item;
        if (slot.Source != SlotSource.Bags)
        {
            return VendorReason.NotInBags;
        }

        if (slot.IsProtected)
        {
            return VendorReason.ProtectedByAutoRetainer;
        }

        if (item.IsEquippable || slot.InGearSet)
        {
            return VendorReason.Gear;
        }

        if (slot.IsHq)
        {
            return VendorReason.HighQuality;
        }

        if (item.IsCollectable || slot.SpiritbondOrCollectability != 0)
        {
            return VendorReason.Collectable;
        }

        return rules.NeverSell.Contains(slot.ItemId) ? VendorReason.NeverSell : null;
    }
}
