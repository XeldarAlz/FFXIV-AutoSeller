using System.Collections.Immutable;

namespace AutoSeller.Core.Pricing.Model;

internal readonly record struct AltSellers(ImmutableArray<ulong> RetainerIds, ImmutableArray<ulong> ContentIds)
{
    public static AltSellers None => new([], []);
}
