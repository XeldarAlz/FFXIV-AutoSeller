using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Execution.Model;

internal static class Fingerprint
{
    public const ulong Empty = 14_695_981_039_346_656_037;

    private const ulong Prime = 1_099_511_628_211;
    private const int BitsPerByte = 8;
    private const int BytesPerValue = sizeof(ulong);
    private const ulong ByteMask = 0xFF;

    public static ulong Mix(ulong hash, ulong value)
    {
        for (var byteIndex = 0; byteIndex < BytesPerValue; byteIndex++)
        {
            hash ^= (value >> (byteIndex * BitsPerByte)) & ByteMask;
            hash = unchecked(hash * Prime);
        }

        return hash;
    }

    public static ulong OfRules(IReadOnlyList<ItemRule> rules)
    {
        ulong combined = 0;
        for (var index = 0; index < rules.Count; index++)
        {
            combined = unchecked(combined + OfRule(rules[index]));
        }

        return Mix(Mix(Empty, combined), (ulong)rules.Count);
    }

    public static ulong OfRule(ItemRule rule)
    {
        var hash = Mix(Empty, rule.ItemId);
        hash = Mix(hash, (ulong)rule.Qualities);
        hash = Mix(hash, rule.Floor);
        hash = Mix(hash, rule.Ceiling);
        hash = Mix(hash, rule.FixedPrice);
        hash = Mix(hash, rule.LotSize);
        hash = Mix(hash, rule.KeepCount);
        hash = Mix(hash, rule.ListingsToKeepUp);
        hash = Mix(hash, rule.PreferredRetainerId);
        return Mix(hash, rule.Paused ? 1UL : 0UL);
    }
}
