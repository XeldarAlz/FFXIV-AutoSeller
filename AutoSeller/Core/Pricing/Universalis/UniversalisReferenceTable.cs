using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace AutoSeller.Core.Pricing.Universalis;

internal sealed class UniversalisReferenceTable
{
    public required UniversalisScope Scope { get; init; }

    public required ImmutableArray<UniversalisReference> Items { get; init; }

    public bool TryGet(uint itemId, out UniversalisReference reference)
    {
        var low = 0;
        var high = Items.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var candidate = Items[middle].ItemId;
            if (candidate == itemId)
            {
                reference = Items[middle];
                return true;
            }

            if (candidate < itemId)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        reference = default;
        return false;
    }

    public static UniversalisReferenceTable Merge(
        UniversalisReferenceTable? previous,
        UniversalisScope scope,
        IReadOnlyList<ReferenceChunkResult> results)
    {
        var merged = new Dictionary<uint, UniversalisReference>();
        if (previous is not null && previous.Scope == scope)
        {
            var carried = previous.Items;
            for (var index = 0; index < carried.Length; index++)
            {
                merged[carried[index].ItemId] = carried[index];
            }
        }

        var answered = new HashSet<uint>();
        for (var resultIndex = 0; resultIndex < results.Count; resultIndex++)
        {
            var items = results[resultIndex].Items;
            for (var itemIndex = 0; itemIndex < items.Length; itemIndex++)
            {
                var fresh = items[itemIndex];
                if (!fresh.Failed)
                {
                    merged[fresh.ItemId] = fresh;
                    answered.Add(fresh.ItemId);
                    continue;
                }

                if (answered.Contains(fresh.ItemId))
                {
                    continue;
                }

                merged[fresh.ItemId] = merged.TryGetValue(fresh.ItemId, out var kept)
                    ? kept with { Coverage = kept.Coverage | (fresh.Coverage & UniversalisReference.FailureCoverage) }
                    : fresh;
            }
        }

        var built = new UniversalisReference[merged.Count];
        merged.Values.CopyTo(built, 0);
        Array.Sort(built, CompareItems);
        return new UniversalisReferenceTable
        {
            Scope = scope,
            Items = ImmutableCollectionsMarshal.AsImmutableArray(built),
        };
    }

    private static int CompareItems(UniversalisReference left, UniversalisReference right)
    {
        return left.ItemId.CompareTo(right.ItemId);
    }
}
