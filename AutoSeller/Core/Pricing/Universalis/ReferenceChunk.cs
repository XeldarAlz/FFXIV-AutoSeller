using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace AutoSeller.Core.Pricing.Universalis;

internal sealed record ReferenceChunk(UniversalisScope Scope, ImmutableArray<uint> ItemIds)
{
    public static ImmutableArray<ReferenceChunk> Split(UniversalisScope scope, ReadOnlySpan<uint> itemIds)
    {
        var distinct = itemIds.ToArray();
        Array.Sort(distinct);
        var count = 0;
        for (var index = 0; index < distinct.Length; index++)
        {
            if (count > 0 && distinct[count - 1] == distinct[index])
            {
                continue;
            }

            distinct[count] = distinct[index];
            count++;
        }

        var chunks = new ReferenceChunk[(count + UniversalisApi.MaximumIdsPerRequest - 1) / UniversalisApi.MaximumIdsPerRequest];
        for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
        {
            var start = chunkIndex * UniversalisApi.MaximumIdsPerRequest;
            var length = Math.Min(UniversalisApi.MaximumIdsPerRequest, count - start);
            chunks[chunkIndex] = new ReferenceChunk(scope, ImmutableArray.Create(distinct, start, length));
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(chunks);
    }
}
