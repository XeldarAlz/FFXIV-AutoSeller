using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Driver.Model;

internal readonly record struct SellListRow(string Name, uint Quantity, uint UnitPrice, int Slot = SellListRows.NoSlot)
{
    public bool IsHq => HighQualityGlyph.IsShownIn(Name);

    public bool HasSlot => Slot != SellListRows.NoSlot;
}

internal static class SellListRows
{
    public const int FirstValue = 10;
    public const int ValuesPerRow = 13;
    public const int NameOffset = 1;
    public const int QuantityOffset = 2;
    public const int PriceOffset = 3;
    public const int NoRow = -1;
    public const int NoSlot = -1;

    public static int ValueIndex(int row, int offset)
    {
        return FirstValue + row * ValuesPerRow + offset;
    }

    public static uint ParsePrice(ReadOnlySpan<char> shown)
    {
        ulong price = 0;
        var digits = 0;
        for (var index = 0; index < shown.Length; index++)
        {
            var character = shown[index];
            if (!char.IsAsciiDigit(character))
            {
                continue;
            }

            digits++;
            price = Math.Min(price * 10 + (ulong)(character - '0'), MarketLimits.MaximumUnitPrice + 1UL);
        }

        return digits == 0 || price > MarketLimits.MaximumUnitPrice ? 0 : (uint)price;
    }

    public static int RowFor(ReadOnlySpan<SellListRow> rows, ReadOnlySpan<ListingSlot> listings, in ListingSlot listing, string itemName)
    {
        var position = PositionOf(listings, listing);
        if (position == NoRow)
        {
            return NoRow;
        }

        if (rows.IsEmpty)
        {
            return position;
        }

        if (SlotsMatch(rows, listings))
        {
            var row = RowAtSlot(rows, listing.Slot);
            return row != NoRow && Shows(rows[row], listing, itemName) ? row : NoRow;
        }

        if (position < rows.Length && Shows(rows[position], listing, itemName))
        {
            return position;
        }

        var found = NoRow;
        for (var index = 0; index < rows.Length; index++)
        {
            if (!Shows(rows[index], listing, itemName))
            {
                continue;
            }

            if (found != NoRow)
            {
                return NoRow;
            }

            found = index;
        }

        return found;
    }

    public static int ShownRowFor(ReadOnlySpan<SellListRow> rows, ReadOnlySpan<ListingSlot> listings, in ListingSlot listing, string itemName)
    {
        return rows.IsEmpty ? NoRow : RowFor(rows, listings, listing, itemName);
    }

    public static bool ShowsAt(ReadOnlySpan<SellListRow> rows, ReadOnlySpan<ListingSlot> listings, int row, in ListingSlot listing, string itemName)
    {
        if (row < 0 || row >= rows.Length || !Shows(rows[row], listing, itemName))
        {
            return false;
        }

        return !SlotsMatch(rows, listings) || rows[row].Slot == listing.Slot;
    }

    public static bool Shows(in SellListRow row, in ListingSlot listing, string itemName)
    {
        return ShowsLot(row, listing) && ItemNameMatch.Matches(row.Name, itemName);
    }

    public static bool SlotsMatch(ReadOnlySpan<SellListRow> rows, ReadOnlySpan<ListingSlot> listings)
    {
        if (rows.IsEmpty || rows.Length != listings.Length)
        {
            return false;
        }

        var seen = 0u;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (!row.HasSlot || row.Slot >= RetainerSnapshot.SlotCount)
            {
                return false;
            }

            var bit = 1u << row.Slot;
            if ((seen & bit) != 0 || !TryFindSlot(listings, row.Slot, out var listing) || !ShowsLot(row, listing))
            {
                return false;
            }

            seen |= bit;
        }

        return true;
    }

    private static bool ShowsLot(in SellListRow row, in ListingSlot listing)
    {
        return row.IsHq == listing.IsHq
            && row.Quantity == listing.Quantity
            && row.UnitPrice == listing.UnitPrice;
    }

    private static int RowAtSlot(ReadOnlySpan<SellListRow> rows, int slot)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].Slot == slot)
            {
                return index;
            }
        }

        return NoRow;
    }

    private static bool TryFindSlot(ReadOnlySpan<ListingSlot> listings, int slot, out ListingSlot listing)
    {
        for (var index = 0; index < listings.Length; index++)
        {
            if (listings[index].Slot == slot)
            {
                listing = listings[index];
                return true;
            }
        }

        listing = default;
        return false;
    }

    private static int PositionOf(ReadOnlySpan<ListingSlot> listings, in ListingSlot listing)
    {
        for (var index = 0; index < listings.Length; index++)
        {
            if (listings[index] == listing)
            {
                return index;
            }
        }

        return NoRow;
    }
}
