using AutoSeller.Core.Driver.Model;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal static class RetainerWindows
{
    public const ChainWindow AboveSellList = ChainWindow.InputNumeric | ChainWindow.ItemSearchResult | ChainWindow.ContextMenu | ChainWindow.RetainerSell;
    public const ChainWindow SettledAfterStop = AboveSellList | ChainWindow.RetainerSellList;

    private const int PollMs = 100;
    private const int PollFrames = 2;

    public static async Task<bool> CloseOwned(ChainWindow windows, int boundMs, Func<bool> stopped)
    {
        var deadline = Environment.TickCount64 + boundMs;
        while (Environment.TickCount64 < deadline && !stopped())
        {
            var next = ChainUnwind.NextToClose(UnexpectedAddons.View().Visible & windows);
            if (next == ChainWindow.None)
            {
                return true;
            }

            CloseOne(next);
            await Delay(PollMs, stopped);
        }

        var left = UnexpectedAddons.View().Visible & windows;
        if (left != ChainWindow.None)
        {
            RunLog.Warning($"Retainer windows: {left} still open after {boundMs} ms.");
        }

        return left == ChainWindow.None;
    }

    public static async Task<bool> RepeatUntil(Func<bool> click, Func<bool> done, int timeoutMs, Func<bool> stopped)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline && !stopped())
        {
            if (done())
            {
                return true;
            }

            click();
            await Delay(PollMs, stopped);
        }

        return !stopped() && done();
    }

    private static void CloseOne(ChainWindow window)
    {
        switch (window)
        {
            case ChainWindow.RetainerSell:
                if (!RetainerClicks.CancelSell())
                {
                    RetainerClicks.CloseSellWindow();
                }

                break;
            case ChainWindow.ItemSearchResult:
                RetainerClicks.CloseResultWindow();
                break;
            case ChainWindow.ContextMenu:
                RetainerClicks.CloseContextMenu();
                break;
            case ChainWindow.RetainerSellList:
                RetainerClicks.CloseSellList();
                break;
            default:
                RetainerClicks.CloseAddon(ChainWindows.NameOf(window));
                break;
        }
    }

    private static async Task Delay(int milliseconds, Func<bool> stopped)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline && !stopped())
        {
            await Svc.Framework.DelayTicks(PollFrames);
        }
    }
}
