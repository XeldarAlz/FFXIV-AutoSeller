using System.Collections.Immutable;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct KnownAlt(ulong ContentId, ImmutableArray<ulong> RetainerIds);

internal readonly record struct AltSellers(ImmutableArray<ulong> RetainerIds, ImmutableArray<ulong> ContentIds)
{
    public static AltSellers None => new([], []);

    public static AltSellers Of(ulong ownContentId, ReadOnlySpan<KnownAlt> alts)
    {
        var retainerCount = 0;
        for (var index = 0; index < alts.Length; index++)
        {
            retainerCount += alts[index].RetainerIds.IsDefault ? 0 : alts[index].RetainerIds.Length;
        }

        var contentIds = new ulong[alts.Length];
        var retainerIds = new ulong[retainerCount];
        var contentCount = 0;
        retainerCount = 0;
        for (var index = 0; index < alts.Length; index++)
        {
            var alt = alts[index];
            if (alt.ContentId == ownContentId)
            {
                continue;
            }

            contentIds[contentCount] = alt.ContentId;
            contentCount++;
            if (alt.RetainerIds.IsDefault)
            {
                continue;
            }

            alt.RetainerIds.AsSpan().CopyTo(retainerIds.AsSpan(retainerCount));
            retainerCount += alt.RetainerIds.Length;
        }

        return new AltSellers(SortedIds.Of<ulong>(retainerIds.AsSpan(0, retainerCount)), SortedIds.Of<ulong>(contentIds.AsSpan(0, contentCount)));
    }

    public bool SameAs(in AltSellers other)
    {
        return RetainerIds.AsSpan().SequenceEqual(other.RetainerIds.AsSpan()) && ContentIds.AsSpan().SequenceEqual(other.ContentIds.AsSpan());
    }
}
