using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace AutoSeller.Core.Pricing.Universalis;

internal readonly record struct ReferenceChunkResult(ReferenceChunk Chunk, UniversalisFailure Failure, ImmutableArray<UniversalisReference> Items)
{
    public bool Failed => Failure != UniversalisFailure.None;

    public static ReferenceChunkResult FailedWith(ReferenceChunk chunk, UniversalisFailure failure)
    {
        var ids = chunk.ItemIds;
        var items = new UniversalisReference[ids.Length];
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = UniversalisReference.WithoutData(ids[index], ReferenceCoverage.RequestFailed);
        }

        return new ReferenceChunkResult(chunk, failure, ImmutableCollectionsMarshal.AsImmutableArray(items));
    }
}
