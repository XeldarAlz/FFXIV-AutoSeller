using System.Collections.Immutable;

namespace AutoSeller.Core.Listings.Model;

internal enum ListingSort : byte
{
    Value,
    Age,
}

internal readonly record struct ListingFilter(ulong ContentId, ulong RetainerId, ListingState? State, string ItemText, ListingSort Sort)
{
    public const ulong Everyone = 0;

    public static ListingFilter All => new(Everyone, Everyone, null, string.Empty, ListingSort.Value);
}

internal static class ListingQuery
{
    public static int Apply(ImmutableArray<ListingRow> rows, ReadOnlySpan<string> itemNames, in ListingFilter filter, Span<int> destination)
    {
        var count = 0;
        for (var index = 0; index < rows.Length && count < destination.Length; index++)
        {
            if (Matches(rows[index], itemNames[index], filter))
            {
                destination[count] = index;
                count++;
            }
        }

        destination[..count].Sort(new Order(rows, filter.Sort));
        return count;
    }

    public static bool Matches(in ListingRow row, string itemName, in ListingFilter filter)
    {
        if (filter.ContentId != ListingFilter.Everyone && row.ContentId != filter.ContentId)
        {
            return false;
        }

        if (filter.RetainerId != ListingFilter.Everyone && row.RetainerId != filter.RetainerId)
        {
            return false;
        }

        if (filter.State is { } state && row.State != state)
        {
            return false;
        }

        return filter.ItemText.Length == 0 || itemName.Contains(filter.ItemText, StringComparison.OrdinalIgnoreCase);
    }

    public static int CountIn(ReadOnlySpan<ListingRow> rows, ListingState state)
    {
        var count = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].State == state)
            {
                count++;
            }
        }

        return count;
    }

    private readonly struct Order(ImmutableArray<ListingRow> rows, ListingSort sort) : IComparer<int>
    {
        public int Compare(int left, int right)
        {
            var leftRow = rows[left];
            var rightRow = rows[right];
            var order = sort == ListingSort.Age
                ? leftRow.PricedSinceUtc.CompareTo(rightRow.PricedSinceUtc)
                : rightRow.Value.CompareTo(leftRow.Value);
            return order != 0 ? order : left.CompareTo(right);
        }
    }
}
