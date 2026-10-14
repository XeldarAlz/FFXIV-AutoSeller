using AutoSeller.Core.Characters;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers;
using AutoSeller.Core.Shared;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Pricing;

internal sealed class OwnSellersProvider
{
    private const int StackRetainerLimit = MarketLimits.MaximumRetainers * 2;

    private readonly RetainerReader retainers;
    private readonly CharacterStore characters;

    public OwnSellersProvider(RetainerReader retainers, CharacterStore characters)
    {
        this.retainers = retainers;
        this.characters = characters;
    }

    public AltSellers Alts { get; private set; } = AltSellers.None;

    public void UseAlts(AltSellers alts)
    {
        Alts = alts;
        RunLog.Info($"Own sellers: {alts.RetainerIds.Length} alt retainer(s) and {alts.ContentIds.Length} alt character(s) now count as own.");
    }

    public OwnSellers Current()
    {
        var live = retainers.Last;
        var stored = characters.Current?.Retainers;
        var storedCount = stored?.Count ?? 0;
        var total = live.Length + storedCount;
        Span<ulong> retainerIds = total <= StackRetainerLimit ? stackalloc ulong[StackRetainerLimit] : new ulong[total];
        for (var index = 0; index < live.Length; index++)
        {
            retainerIds[index] = live[index].RetainerId;
        }

        for (var index = 0; index < storedCount; index++)
        {
            retainerIds[live.Length + index] = stored![index].RetainerId;
        }

        return OwnSellers.Of(retainerIds[..total], Svc.PlayerState.ContentId, Alts);
    }
}
