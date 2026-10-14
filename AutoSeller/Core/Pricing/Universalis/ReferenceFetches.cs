using System.Collections.Immutable;

namespace AutoSeller.Core.Pricing.Universalis;

internal readonly record struct ReferenceFetchTicket(long Generation, UniversalisScope Scope, ImmutableArray<ReferenceChunk> Chunks, int ItemCount, int CarriedCount);

internal sealed class ReferenceFetches
{
    private ImmutableArray<uint> inFlightItemIds = [];
    private UniversalisScope inFlightScope;

    public long Generation { get; private set; }

    public bool InFlight => !inFlightItemIds.IsEmpty;

    public ReferenceFetchTicket Begin(UniversalisScope scope, ReadOnlySpan<uint> itemIds)
    {
        var carried = InFlight && inFlightScope == scope ? inFlightItemIds.AsSpan() : [];
        var requested = new uint[carried.Length + itemIds.Length];
        carried.CopyTo(requested);
        itemIds.CopyTo(requested.AsSpan(carried.Length));
        var chunks = ReferenceChunk.Split(scope, requested);
        var distinct = DistinctIds(chunks);
        Generation++;
        inFlightScope = scope;
        inFlightItemIds = distinct;
        return new ReferenceFetchTicket(Generation, scope, chunks, distinct.Length, carried.Length);
    }

    public bool TryFinish(long generation)
    {
        if (generation != Generation)
        {
            return false;
        }

        inFlightItemIds = [];
        return true;
    }

    public void Reset()
    {
        Generation++;
        inFlightItemIds = [];
    }

    private static ImmutableArray<uint> DistinctIds(ImmutableArray<ReferenceChunk> chunks)
    {
        var count = 0;
        for (var index = 0; index < chunks.Length; index++)
        {
            count += chunks[index].ItemIds.Length;
        }

        var builder = ImmutableArray.CreateBuilder<uint>(count);
        for (var index = 0; index < chunks.Length; index++)
        {
            builder.AddRange(chunks[index].ItemIds);
        }

        return builder.MoveToImmutable();
    }
}
