using System.Globalization;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing;
using AutoSeller.Core.Tasks;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Market;

internal static class MarketDebugCommands
{
    public const string LookSubcommand = "look";
    public const string StatusSubcommand = "mbstatus";

    public static void Look(string arguments, Plugin plugin)
    {
        if (!uint.TryParse(arguments, NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) || itemId == 0)
        {
            PrintError(Loc.T(L.Market.LookUsage));
            return;
        }

        if (!Svc.ClientState.IsLoggedIn)
        {
            PrintError(Loc.T(L.Market.LookLoggedOut));
            return;
        }

        if (plugin.Controller.Running || clib.Services.Svc.Automation.Running)
        {
            PrintError(Loc.T(L.Market.LookBusy));
            return;
        }

        if (!plugin.Market.Gates.Allows(MarketGate.ReadListings))
        {
            PrintError(Loc.T(L.Market.LookReadingOff));
            return;
        }

        var item = plugin.GameItems.Get(itemId);
        if (item.Facts.IsUntradable || item.Facts.ItemSearchCategory == 0)
        {
            PrintError(Loc.T(L.Market.LookNotMarketable, itemId));
            return;
        }

        var name = MarketLookTask.NameOf(item.Name, itemId);
        var throughSellWindow = ListingRequestTracker.SellWindowReady && plugin.Configuration.SearchSendPath == SearchSendPath.ComparePrices;
        if (TaskStarter.TryStart(() => new MarketLookTask(itemId, plugin)))
        {
            Print(Loc.T(throughSellWindow ? L.Market.LookThroughSellWindow : L.Market.LookThroughProxy, name));
        }
    }

    public static void PrintStatus(Plugin plugin)
    {
        var gates = plugin.Market.Gates;
        RunLog.Info($"Status: reading listings {OnOff(gates.Allows(MarketGate.ReadListings))}, reading the retainer market {OnOff(gates.Allows(MarketGate.ReadRetainerMarket))}, writing prices {OnOff(gates.Allows(MarketGate.WritePrices))}; first failed check: {(gates.FirstFailure.Length == 0 ? "none" : gates.FirstFailure)}.");
        for (var index = 0; index < gates.Checks.Length; index++)
        {
            var check = gates.Checks[index];
            RunLog.Info($"Status: check {check.Name}: {(check.Passed ? "ok" : "FAILED")}.");
        }

        WriteSearch(plugin);
        WriteReferences(plugin.References);
        WriteSellers(plugin);
        Print(Loc.T(
            L.Market.StatusSummary,
            GateWord(gates.Allows(MarketGate.ReadListings)),
            GateWord(gates.Allows(MarketGate.ReadRetainerMarket)),
            GateWord(gates.Allows(MarketGate.WritePrices))));
    }

    public static void Print(string message)
    {
        Svc.Chat.Print($"{AsellConstants.LogPrefix} {message}");
    }

    public static void PrintError(string message)
    {
        Svc.Chat.PrintError($"{AsellConstants.LogPrefix} {message}");
    }

    private static void WriteSearch(Plugin plugin)
    {
        var tracker = plugin.Market.Tracker;
        var state = tracker.State;
        RunLog.Info($"Status: search {state.Status} for item {state.Item.ItemId} {(state.Item.IsHq ? "HQ" : "NQ")}, generation {state.Generation}, attempts {tracker.Attempts}, failures {tracker.Failures}, failure {state.Failure}, last failure {tracker.LastFailure}, next send in {tracker.NextSendInMs} ms, reused {state.Reused}.");
        RunLog.Info($"Status: send path {plugin.Configuration.SearchSendPath}, sell window open {ListingRequestTracker.SellWindowReady}, result window left open by the plugin {tracker.OwnsResultWindow}.");
        if (state.Snapshot is { } snapshot)
        {
            RunLog.Info($"Status: last search of item {snapshot.ItemId}: {snapshot.Listings.Length} listing(s), {snapshot.History.Length} sale(s), generation {snapshot.Generation}, captured {(int)(DateTime.UtcNow - snapshot.CapturedAtUtc).TotalSeconds} s ago.");
        }
    }

    private static void WriteReferences(ReferenceService references)
    {
        var cooldown = (int)Math.Ceiling(references.CooldownRemaining.TotalSeconds);
        if (references.Latest is not { } latest)
        {
            RunLog.Info($"Status: references not fetched yet; fetching {references.Fetching}, cooldown {cooldown} s.");
            return;
        }

        var taxes = latest.Taxes.Failed ? $"tax rates missing ({latest.Taxes.Failure})" : "tax rates read";
        RunLog.Info($"Status: references generation {latest.Generation} on {latest.Scope.Segment}, {(int)latest.Age(DateTime.UtcNow).TotalSeconds} s old, {latest.Table.Items.Length} item(s), {latest.FailedRequestCount} of {latest.RequestCount} request(s) failed, {taxes}; fetching {references.Fetching}, cooldown {cooldown} s.");
    }

    private static void WriteSellers(Plugin plugin)
    {
        var sellers = plugin.OwnSellers.Current();
        var open = plugin.Retainers.TryGetOpenRetainer(out var retainer) ? retainer.RetainerId.ToString(CultureInfo.InvariantCulture) : "none";
        RunLog.Info($"Status: retainer reading {OnOff(plugin.Retainers.Available)}, {plugin.Retainers.Last.Length} retainer(s) read this session, open retainer {open}; own sellers {sellers.RetainerCount} retainer(s) and {sellers.CharacterCount} character(s), alts {plugin.OwnSellers.Alts.RetainerIds.Length} retainer(s).");
    }

    private static string OnOff(bool open)
    {
        return open ? "on" : "off";
    }

    private static string GateWord(bool open)
    {
        return Loc.T(open ? L.Market.StatusOn : L.Market.StatusOff);
    }
}
