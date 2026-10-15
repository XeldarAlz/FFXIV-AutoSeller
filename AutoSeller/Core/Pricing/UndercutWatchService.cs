using AutoSeller.Core.Characters;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Sales.Model;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Pricing;

internal sealed class UndercutWatchService : IDisposable
{
    private readonly Configuration configuration;
    private readonly CharacterStore characters;
    private readonly ReferenceService references;
    private readonly Func<bool> runActive;
    private long lastCheckMs = WatchInterval.NeverChecked;
    private long trackedGeneration;

    public UndercutWatchService(Configuration configuration, CharacterStore characters, ReferenceService references, Func<bool> runActive)
    {
        this.configuration = configuration;
        this.characters = characters;
        this.references = references;
        this.runActive = runActive;
        Svc.Framework.Update += OnUpdate;
        Svc.ClientState.Logout += OnLogout;
    }

    public DateTime LastCheckUtc { get; private set; }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.ClientState.Logout -= OnLogout;
    }

    private void OnUpdate(IFramework framework)
    {
        if (!Svc.ClientState.IsLoggedIn || characters.Current is not { } data)
        {
            return;
        }

        TrackLowestPrices(data);
        var now = Environment.TickCount64;
        if (!WatchInterval.IsDue(lastCheckMs, now, configuration.UndercutWatchMinutes) || references.Fetching || runActive())
        {
            return;
        }

        lastCheckMs = now;
        var items = PlanStock.ItemsToPrice([], data.SnapshotArray());
        if (items.IsEmpty)
        {
            return;
        }

        LastCheckUtc = DateTime.UtcNow;
        var fetch = references.Fetch(items.AsSpan());
        RunLog.Info($"Undercut watch: checking {items.Length} listed item(s) against Universalis, every {configuration.UndercutWatchMinutes} minutes: {fetch}.");
    }

    private void TrackLowestPrices(CharacterData data)
    {
        if (references.Current is not { } current || current.Generation == trackedGeneration)
        {
            return;
        }

        trackedGeneration = current.Generation;
        var nowUtc = DateTime.UtcNow;
        var snapshots = data.Snapshots;
        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
        {
            var snapshot = snapshots[snapshotIndex];
            var slots = snapshot.Slots;
            for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                var listing = slots[slotIndex];
                if (!MarketEstimate.TryWorldLowest(current, listing.Key, nowUtc, out var lowest))
                {
                    continue;
                }

                var key = new RepriceKey(snapshot.RetainerId, listing.Slot, listing.Key);
                var watched = new WatchedListing(snapshot.RetainerId, listing.Slot, listing.Key, listing.UnitPrice, data.ListedSince(snapshot.RetainerId, listing, snapshot.ReadAtUtc), data.RepriceEntryFor(key).LastRepriceUtc);
                data.TrackLowestPrice(key, watched.UnitPrice, watched.PricedAtUtc, LowestPriceCheck.Of(lowest));
            }
        }
    }

    private void OnLogout(int type, int code)
    {
        lastCheckMs = WatchInterval.NeverChecked;
        trackedGeneration = 0;
        LastCheckUtc = default;
    }
}
