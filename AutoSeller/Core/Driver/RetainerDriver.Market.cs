using System.Text;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Market;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Retainers;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Core.Tasks.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private const int MarketReadMs = 10_000;
    private const int MarketPollFrames = 6;
    private const int SearchBudgetMs = 45_000;
    private const int ResultWindowCloseMs = 5_000;
    private const int SellWindowCloseMs = 5_000;
    private const int UnwindBoundMs = 10_000;
    private const ChainWindow SearchWindows = SellListBackdrop | ChainWindow.ItemSearchResult;

    private async Task<RetainerSnapshot?> ReadMarket(RunRetainer retainer)
    {
        var deadline = Environment.TickCount64 + MarketReadMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            var read = plugin.RetainerMarket.ReadOpenRetainer(DateTime.UtcNow);
            if (read.Status == MarketReadStatus.Read && read.RetainerId == retainer.RetainerId && read.Snapshot is { } snapshot)
            {
                ReportDiff(retainer, read.Previous, read.Diff, snapshot);
                return snapshot;
            }

            await NextFrame(MarketPollFrames);
        }

        return null;
    }

    private void ReportDiff(RunRetainer retainer, RetainerSnapshot? previous, SnapshotDiff? diff, RetainerSnapshot current)
    {
        if (previous is null || diff is null)
        {
            Diag($"Driver: {retainer.Name} has no earlier snapshot to compare with; {current.Slots.Length} listing(s) now.");
            return;
        }

        var line = new StringBuilder()
            .Append("Driver: ").Append(retainer.Name).Append(" since ").Append(RunLog.Time(previous.ReadAtUtc)).Append(": ")
            .Append(diff.Changes.Length).Append(" change(s), gil ").Append(diff.GilGrowth)
            .Append(", inferred sold ").Append(diff.InferredSoldGross).Append(" gil, ").Append(diff.Corroboration).Append('.');
        var changes = diff.Changes;
        for (var index = 0; index < changes.Length; index++)
        {
            var change = changes[index];
            line.Append(" [").Append(change.Kind)
                .Append(": slot ").Append(change.Listing.Slot)
                .Append(" item ").Append(change.Listing.ItemId)
                .Append(' ').Append(QualityLabel.Of(change.Listing.IsHq))
                .Append(" x").Append(change.Listing.Quantity)
                .Append(" at ").Append(change.Listing.UnitPrice)
                .Append(" from ").Append(change.PreviousUnitPrice)
                .Append(']');
        }

        Diag(line.ToString());
    }

    private async Task<SearchResult> Compare(ItemKey item, bool allowReuse)
    {
        var tracker = plugin.Market.Tracker;
        var state = tracker.Request(item, allowReuse, SearchSendPath.ComparePrices);
        var watch = ChainStep.Closes(ChainWindow.RetainerSell, SearchBudgetMs, SearchWindows);
        chain.Begin(watch, Environment.TickCount64);
        try
        {
            while (state.Status == ListingRequestStatus.Waiting)
            {
                await NextFrame();
                state = tracker.Poll();
                if (!decider.SameCharacter())
                {
                    return SearchResult.Halt(new Stop(DriverHalt.OtherCharacter, "another character than the authorized one is logged in during the price comparison"));
                }

                if (UnexpectedAddons.InCombat())
                {
                    return SearchResult.Halt(new Stop(DriverHalt.CombatOrDutyPop, "combat during the price comparison"));
                }

                var verdict = chain.Poll(UnexpectedAddons.View(), Environment.TickCount64);
                switch (verdict.Action)
                {
                    case ChainAction.Halt:
                        return SearchResult.Halt(await HaltFor(verdict, chain.Step));
                    case ChainAction.Pause:
                        return SearchResult.Halt(new Stop(DriverHalt.CombatOrDutyPop, $"{verdict.Window} opened during the price comparison"));
                    case ChainAction.Next:
                        chain.Begin(watch, Environment.TickCount64);
                        break;
                    case ChainAction.Unwind:
                        Warn($"Driver: the price comparison of item {item.ItemId} stopped waiting ({verdict.Window})");
                        return SearchResult.Failed;
                }
            }
        }
        finally
        {
            tracker.Cancel();
        }

        if (!await CloseResultWindow(tracker))
        {
            tracker.ReleaseResultWindow();
        }

        if (state.Snapshot is not { } snapshot)
        {
            Warn($"Driver: the price comparison of item {item.ItemId} ended {state.Status} ({state.Failure})");
            return SearchResult.Failed;
        }

        return new SearchResult(snapshot, Stop.None);
    }

    private async Task<bool> CloseResultWindow(ListingRequestTracker tracker)
    {
        var deadline = Environment.TickCount64 + ResultWindowCloseMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (tracker.CloseResultWindow())
            {
                return true;
            }

            await NextFrame(StepPollFrames);
        }

        return false;
    }

    private async Task<Stop> CancelSellWindow()
    {
        var closed = await Step(ChainStep.Closes(ChainWindow.RetainerSell, SellWindowCloseMs, SearchWindows), RetainerClicks.CancelSell);
        if (closed.End is StepEnd.Done or StepEnd.Cancelled)
        {
            return Stop.None;
        }

        if (closed.Halted)
        {
            return closed.Stop;
        }

        return await UnwindAboveSellList();
    }

    private async Task<Stop> UnwindAboveSellList()
    {
        if (await RetainerWindows.CloseOwned(RetainerWindows.AboveSellList, UnwindBoundMs, () => CancelToken.IsCancellationRequested) || CancelToken.IsCancellationRequested)
        {
            return Stop.None;
        }

        return new Stop(DriverHalt.WindowsDidNotClose, "a sell window, a context menu or the search results did not close");
    }

    private void Record(in RunDecision row, RunRetainer retainer)
    {
        session.Record(row);
        var line = decider.Describe(row, retainer.Name);
        if (row.Skip == RunSkip.None)
        {
            Diag(line);
            return;
        }

        Warn(line);
    }

    private readonly record struct SearchResult(CompetitorSnapshot? Snapshot, Stop Stop)
    {
        public static SearchResult Failed => new(null, Stop.None);

        public static SearchResult Halt(in Stop stop) => new(null, stop);
    }
}
