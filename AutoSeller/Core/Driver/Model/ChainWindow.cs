using System.Collections.Immutable;

namespace AutoSeller.Core.Driver.Model;

[Flags]
internal enum ChainWindow : uint
{
    None = 0,
    ContentsFinderConfirm = 1 << 0,
    SelectOk = 1 << 1,
    Request = 1 << 2,
    SelectIconString = 1 << 3,
    ContextIconMenu = 1 << 4,
    SelectYesno = 1 << 5,
    RetainerList = 1 << 6,
    Talk = 1 << 7,
    SelectString = 1 << 8,
    RetainerHistory = 1 << 9,
    RetainerSellList = 1 << 10,
    ContextMenu = 1 << 11,
    RetainerSell = 1 << 12,
    ItemSearchResult = 1 << 13,
    InputNumeric = 1 << 14,
}

internal readonly record struct TrackedWindow(ChainWindow Window, string AddonName);

internal static class ChainWindows
{
    public static readonly ImmutableArray<TrackedWindow> Tracked =
    [
        new(ChainWindow.ContentsFinderConfirm, nameof(ChainWindow.ContentsFinderConfirm)),
        new(ChainWindow.SelectOk, nameof(ChainWindow.SelectOk)),
        new(ChainWindow.Request, nameof(ChainWindow.Request)),
        new(ChainWindow.SelectIconString, nameof(ChainWindow.SelectIconString)),
        new(ChainWindow.ContextIconMenu, nameof(ChainWindow.ContextIconMenu)),
        new(ChainWindow.SelectYesno, nameof(ChainWindow.SelectYesno)),
        new(ChainWindow.RetainerList, nameof(ChainWindow.RetainerList)),
        new(ChainWindow.Talk, nameof(ChainWindow.Talk)),
        new(ChainWindow.SelectString, nameof(ChainWindow.SelectString)),
        new(ChainWindow.RetainerHistory, nameof(ChainWindow.RetainerHistory)),
        new(ChainWindow.RetainerSellList, nameof(ChainWindow.RetainerSellList)),
        new(ChainWindow.ContextMenu, nameof(ChainWindow.ContextMenu)),
        new(ChainWindow.RetainerSell, nameof(ChainWindow.RetainerSell)),
        new(ChainWindow.ItemSearchResult, nameof(ChainWindow.ItemSearchResult)),
        new(ChainWindow.InputNumeric, nameof(ChainWindow.InputNumeric)),
    ];

    public static bool Contains(ChainWindow set, ChainWindow window) => (set & window) != 0;
}
