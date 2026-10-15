using System.Threading;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ClibServices = clib.Services.Svc;

namespace AutoSeller.Core.Driver;

internal sealed class AssistHost : IDisposable
{
    private const int OriginFreshMs = 5_000;
    private const int RepriceWatchMs = 5_000;

    private readonly Plugin plugin;
    private readonly ListingSlot[] market = new ListingSlot[RetainerSnapshot.SlotCount];
    private AssistReprice expected;
    private long watchUntilMs;
    private bool watching;
    private AssistView view = AssistView.Idle;
    private AssistTask? task;
    private long revision;
    private AssistOrigin menuOrigin = AssistOrigin.Unknown;
    private long menuOpenedAtMs;

    public AssistHost(Plugin plugin)
    {
        this.plugin = plugin;
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, RetainerClicks.SellWindowAddon, OnSellWindowSetup);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, RetainerClicks.SellWindowAddon, OnSellWindowClosing);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, RetainerClicks.ContextMenuAddon, OnContextMenuSetup);
    }

    public AssistView View => Volatile.Read(ref view);

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, RetainerClicks.SellWindowAddon, OnSellWindowSetup);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, RetainerClicks.SellWindowAddon, OnSellWindowClosing);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, RetainerClicks.ContextMenuAddon, OnContextMenuSetup);
        StopWatching();
        var automation = ClibServices.Automation;
        if (ReferenceEquals(automation.CurrentTask, task))
        {
            automation.Stop();
        }
    }

    public void Show(AssistStatus status, string itemName, in AssistSuggestion suggestion, in Decision decision, TimeSpan cooldownLeft)
    {
        Volatile.Write(ref view, new AssistView(Interlocked.Increment(ref revision), status, itemName, suggestion, decision, cooldownLeft));
    }

    public void ExpectReprice(in AssistReprice reprice)
    {
        expected = reprice;
    }

    private void OnContextMenuSetup(AddonEvent type, AddonArgs args)
    {
        if (!plugin.Configuration.AssistEnabled || plugin.Controller.HoldsRetainerWindows)
        {
            return;
        }

        menuOrigin = RetainerReads.MenuOrigin();
        menuOpenedAtMs = Environment.TickCount64;
    }

    private void OnSellWindowSetup(AddonEvent type, AddonArgs args)
    {
        Volatile.Write(ref view, AssistView.Idle);
        if (!watching)
        {
            expected = default;
        }

        var origin = Environment.TickCount64 - menuOpenedAtMs <= OriginFreshMs ? menuOrigin : AssistOrigin.Unknown;
        menuOrigin = AssistOrigin.Unknown;
        var configuration = plugin.Configuration;
        var automation = ClibServices.Automation;
        var block = AssistGate.Check(new AssistFacts(
            configuration.AssistEnabled,
            plugin.Controller.HoldsRetainerWindows,
            automation.Running && !ReferenceEquals(automation.CurrentTask, task),
            SkipKeyHeld(configuration.AssistSkipKey),
            plugin.Market.Gates.Allows(MarketGate.ReadListings)));
        if (block != AssistBlock.None)
        {
            if (block is not (AssistBlock.AssistOff or AssistBlock.RunOwnsWindows))
            {
                RunLog.Info($"Assist: a sell window opened, and Assist leaves it alone ({block}).");
            }

            return;
        }

        RunLog.Info($"Assist: a sell window was opened by hand ({origin.Opening}); its prices are compared.");
        if (TaskStarter.TryStart(() => task = new AssistTask(plugin, this, origin)))
        {
            plugin.Marketbuddy.Hold();
        }
    }

    private void OnSellWindowClosing(AddonEvent type, AddonArgs args)
    {
        Volatile.Write(ref view, AssistView.Idle);
        if (!expected.IsSet || watching)
        {
            return;
        }

        watchUntilMs = Environment.TickCount64 + RepriceWatchMs;
        watching = true;
        Svc.Framework.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        var reprice = expected;
        var listing = reprice.Listing;
        if (plugin.RetainerMarket.TrySample(reprice.RetainerId, market, out var count, out _) && reprice.LandedIn(market.AsSpan(0, count)))
        {
            plugin.Characters.Current?.RecordReprice(reprice.Key, DateTime.UtcNow, listing.UnitPrice, reprice.TypedPrice);
            RunLog.Info($"Assist: slot {listing.Slot} with item {listing.ItemId} {QualityLabel.Of(listing.IsHq)} now shows the {reprice.TypedPrice} gil Assist typed in place of {listing.UnitPrice}; it counts as a reprice for the cooldown and the price war.");
            StopWatching();
            return;
        }

        if (Environment.TickCount64 < watchUntilMs)
        {
            return;
        }

        RunLog.Info($"Assist: slot {listing.Slot} with item {listing.ItemId} {QualityLabel.Of(listing.IsHq)} did not show the {reprice.TypedPrice} gil Assist typed within {RepriceWatchMs} ms of the sell window closing; no reprice is recorded.");
        StopWatching();
    }

    private void StopWatching()
    {
        expected = default;
        watchUntilMs = 0;
        if (!watching)
        {
            return;
        }

        watching = false;
        Svc.Framework.Update -= OnUpdate;
    }

    private static bool SkipKeyHeld(AssistSkipKey key)
    {
        return Svc.KeyState[key switch
        {
            AssistSkipKey.Control => VirtualKey.CONTROL,
            AssistSkipKey.Alt => VirtualKey.MENU,
            _ => VirtualKey.SHIFT,
        }];
    }
}
