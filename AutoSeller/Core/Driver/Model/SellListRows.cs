using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Driver.Model;

internal readonly record struct SellListRow(string Name, uint Quantity, uint UnitPrice)
{
    public bool IsHq => HighQualityGlyph.IsShownIn(Name);
}

internal static class SellListRows
{
    public const int FirstValue = 10;
    public const int ValuesPerRow = 13;
    public const int NameOffset = 1;
    public const int QuantityOffset = 2;
    public const int PriceOffset = 3;
    public const int NoRow = -1;

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

    public static bool Shows(in SellListRow row, in ListingSlot listing, string itemName)
    {
        return row.IsHq == listing.IsHq
            && row.Quantity == listing.Quantity
            && row.UnitPrice == listing.UnitPrice
            && ItemNameMatch.Matches(row.Name, itemName);
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
