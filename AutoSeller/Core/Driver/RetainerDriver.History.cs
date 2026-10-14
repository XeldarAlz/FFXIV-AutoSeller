using System.Collections.Immutable;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Game.Ops;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Sales.Model;
using AutoSeller.Core.Tasks.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private const int SaleHistoryOpenMs = 10_000;
    private const int SaleHistoryReadMs = 3_000;

    private async Task<Stop> ReadSaleHistory(RunRetainer retainer)
    {
        if (!plugin.Configuration.ReadSaleHistory)
        {
            return Stop.None;
        }

        session.Show(RunState.ReadingSaleHistory, Loc.T(L.Run.StatusSaleHistory, retainer.Name));
        var historyText = RetainerMenuText.SaleHistory();
        var entries = NpcInteraction.SelectStringEntries();
        var entry = historyText.IsKnown ? historyText.FirstMatchIn(entries) : PromptText.NoMatch;
        if (entry == PromptText.NoMatch)
        {
            Warn($"Driver: the sale history entry (Addon {RetainerMenuText.SaleHistoryRow}) is not among [{string.Join(" | ", entries)}]; the sale history of {retainer.Name} is not read this visit.");
            return Stop.None;
        }

        var reader = plugin.SaleHistory;
        reader.Arm(retainer.RetainerId);
        try
        {
            var opened = await Step(
                ChainStep.Opens(ChainWindow.RetainerHistory, SaleHistoryOpenMs, RetainerBackdrop | ChainWindow.Talk),
                () => RetainerClicks.OpenSaleHistory(entry, historyText),
                RetainerClicks.ProgressTalk);
            if (opened.Halted || opened.End == StepEnd.Cancelled)
            {
                return opened.Stop;
            }

            if (!opened.Succeeded)
            {
                Warn($"Driver: the sale history of {retainer.Name} did not open; the visit carries on without it.");
                return await BackToRetainerMenu();
            }

            RecordSaleHistory(retainer, await WaitForSaleHistory(retainer));
            return await CloseSaleHistory(retainer);
        }
        finally
        {
            reader.Disarm();
        }
    }

    private async Task<ImmutableArray<SaleRecord>> WaitForSaleHistory(RunRetainer retainer)
    {
        var characterContentId = session.Authorization.CharacterContentId;
        var deadline = Environment.TickCount64 + SaleHistoryReadMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (plugin.SaleHistory.TryRead(characterContentId, retainer.RetainerId, retainer.SellerTaxPercent, DateTime.UtcNow, out var sales))
            {
                return sales;
            }

            await NextFrame(StepPollFrames);
        }

        return default;
    }

    private void RecordSaleHistory(RunRetainer retainer, ImmutableArray<SaleRecord> sales)
    {
        if (sales.IsDefault)
        {
            Warn($"Driver: the sale history of {retainer.Name} could not be read from the {plugin.SaleHistory.Source} within {SaleHistoryReadMs} ms; the Console has its raw values.");
            return;
        }

        var recorded = plugin.Sales.Record(session.Authorization.CharacterContentId, sales.AsSpan());
        Diag($"Driver: the sale history of {retainer.Name} holds {sales.Length} sale(s), {recorded} of them new.");
    }

    private async Task<Stop> CloseSaleHistory(RunRetainer retainer)
    {
        var closed = await Step(ChainStep.Closes(ChainWindow.RetainerHistory, MenuReturnMs, RetainerBackdrop | ChainWindow.Talk), RetainerClicks.CloseSaleHistory, RetainerClicks.ProgressTalk);
        if (!closed.Succeeded)
        {
            return Failed(closed, DriverHalt.WindowsDidNotClose, $"the sale history of {retainer.Name} did not close");
        }

        var menu = await Watch(ChainStep.Opens(ChainWindow.SelectString, MenuReturnMs, ChainWindow.RetainerList | ChainWindow.Talk), RetainerClicks.ProgressTalk);
        return menu.Succeeded ? Stop.None : Failed(menu, DriverHalt.WindowsDidNotClose, $"the menu of {retainer.Name} did not come back after the sale history");
    }
}
