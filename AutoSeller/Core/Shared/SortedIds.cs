using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;

namespace AutoSeller.Core.Shared;

internal static class SortedIds
{
    public static ImmutableArray<T> Of<T>(ReadOnlySpan<T> ids)
        where T : unmanaged, IBinaryInteger<T>
    {
        var sorted = ids.ToArray();
        Array.Sort(sorted);
        var count = 0;
        for (var index = 0; index < sorted.Length; index++)
        {
            var id = sorted[index];
            if (T.IsZero(id) || (count > 0 && sorted[count - 1] == id))
            {
                continue;
            }

            sorted[count] = id;
            count++;
        }

        Array.Resize(ref sorted, count);
        return ImmutableCollectionsMarshal.AsImmutableArray(sorted);
    }
}
