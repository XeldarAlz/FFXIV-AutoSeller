using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace AutoSeller.Core.Pricing.Universalis;

internal sealed class ReferenceChunkResult
{
    public required ReferenceChunk Chunk { get; init; }

    public required UniversalisFailure Failure { get; init; }

    public required ImmutableArray<UniversalisReference> Items { get; init; }

    public bool Failed => Failure != UniversalisFailure.None;

    public static ReferenceChunkResult FailedWith(ReferenceChunk chunk, UniversalisFailure failure)
    {
        var ids = chunk.ItemIds;
        var items = new UniversalisReference[ids.Length];
        for (var index = 0; index < items.Length; index++)
        {
            items[index] = UniversalisReference.WithoutData(ids[index], ReferenceCoverage.RequestFailed);
        }

        return new ReferenceChunkResult
        {
            Chunk = chunk,
            Failure = failure,
            Items = ImmutableCollectionsMarshal.AsImmutableArray(items),
        };
    }
}
