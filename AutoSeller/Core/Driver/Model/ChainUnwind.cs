using System.Collections.Immutable;

namespace AutoSeller.Core.Driver.Model;

internal static class ChainUnwind
{
    private static readonly ImmutableArray<ChainWindow> closeOrder =
    [
        ChainWindow.InputNumeric,
        ChainWindow.ItemSearchResult,
        ChainWindow.ContextMenu,
        ChainWindow.RetainerSell,
        ChainWindow.RetainerHistory,
        ChainWindow.RetainerSellList,
    ];

    public static readonly ChainWindow Owned = Combine(closeOrder);

    public static ChainWindow NextToClose(ChainWindow visible)
    {
        for (var index = 0; index < closeOrder.Length; index++)
        {
            if (ChainWindows.Contains(visible, closeOrder[index]))
            {
                return closeOrder[index];
            }
        }

        return ChainWindow.None;
    }

    private static ChainWindow Combine(ImmutableArray<ChainWindow> windows)
    {
        var combined = ChainWindow.None;
        for (var index = 0; index < windows.Length; index++)
        {
            combined |= windows[index];
        }

        return combined;
    }
}
