using System.Collections.Immutable;
using System.Runtime.InteropServices;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Rules.Model;

internal readonly record struct VendorSettings(bool Enabled, ItemIdSet VendorList, uint JunkThreshold)
{
    public const uint DefaultJunkThreshold = 1_000;

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
}

internal readonly record struct VendorVerdict(SlotFacts Slot, VendorReason Reason, long VendorValue)
{
    public bool IsVendored => Reason is VendorReason.VendorList or VendorReason.Junk;
}

internal static class VendorRules
{
    public static ImmutableArray<VendorVerdict> Plan(ImmutableArray<StockSlot> stock, RuleBook rules, long vendoredValueCap)
    {
        var verdicts = new VendorVerdict[stock.Length];
        long vendoredValue = 0;
        for (var index = 0; index < stock.Length; index++)
        {
            var entry = stock[index];
            var verdict = new VendorVerdict(entry.Slot, Check(entry, rules), VendorValue(entry));
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

    private static VendorReason? Refusal(in StockSlot stock, RuleBook rules)
    {
        var slot = stock.Slot;
        var item = stock.Item;
        if (!rules.Vendor.Enabled)
        {
            return VendorReason.VendoringOff;
        }

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

        if (rules.NeverSell.Contains(slot.ItemId))
        {
            return VendorReason.NeverSell;
        }

        if (rules.IsOnSellList(slot.ItemId))
        {
            return VendorReason.OnSellList;
        }

        if (Eligibility.MatchCategory(rules, item.ItemUiCategory, stock.ReferenceUnitPrice, out _) == EligibilityReason.Eligible)
        {
            return VendorReason.MatchedCategoryRule;
        }

        if (item.VendorPrice == 0)
        {
            return VendorReason.NoVendorPrice;
        }

        return null;
    }
}
