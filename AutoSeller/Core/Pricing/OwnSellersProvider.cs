using AutoSeller.Core.AutoRetainer;
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
    private readonly Configuration configuration;
    private readonly AutoRetainerIpc autoRetainer;
    private AltReadKey lastRead;
    private long altsReadAtMs = AltReadSchedule.NeverRead;

    public OwnSellersProvider(RetainerReader retainers, CharacterStore characters, Configuration configuration, AutoRetainerIpc autoRetainer)
    {
        this.retainers = retainers;
        this.characters = characters;
        this.configuration = configuration;
        this.autoRetainer = autoRetainer;
    }

    public AltSellers Alts { get; private set; } = AltSellers.None;

    public void ReadAlts(bool readNow)
    {
        var read = new AltReadKey(Svc.PlayerState.ContentId, configuration.AutoRetainerAltsAreOwn);
        var now = Environment.TickCount64;
        if (!AltReadSchedule.Due(readNow, read, lastRead, altsReadAtMs, now))
        {
            return;
        }

        lastRead = read;
        altsReadAtMs = now;
        var alts = read.Wanted ? autoRetainer.ReadAlts(read.ContentId) : AltSellers.None;
        if (alts.SameAs(Alts))
        {
            return;
        }

        Alts = alts;
        RunLog.Info(read.Wanted
            ? $"Own sellers: AutoRetainer knows {alts.ContentIds.Length} other character(s) with {alts.RetainerIds.Length} retainer(s); their listings count as own."
            : "Own sellers: AutoRetainer's characters no longer count as alts.");
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
