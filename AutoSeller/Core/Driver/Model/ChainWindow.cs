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
        new(ChainWindow.ContentsFinderConfirm, "ContentsFinderConfirm"),
        new(ChainWindow.SelectOk, "SelectOk"),
        new(ChainWindow.Request, "Request"),
        new(ChainWindow.SelectIconString, "SelectIconString"),
        new(ChainWindow.ContextIconMenu, "ContextIconMenu"),
        new(ChainWindow.SelectYesno, "SelectYesno"),
        new(ChainWindow.RetainerList, "RetainerList"),
        new(ChainWindow.Talk, "Talk"),
        new(ChainWindow.SelectString, "SelectString"),
        new(ChainWindow.RetainerHistory, "RetainerHistory"),
        new(ChainWindow.RetainerSellList, "RetainerSellList"),
        new(ChainWindow.ContextMenu, "ContextMenu"),
        new(ChainWindow.RetainerSell, "RetainerSell"),
        new(ChainWindow.ItemSearchResult, "ItemSearchResult"),
        new(ChainWindow.InputNumeric, "InputNumeric"),
    ];

    public static bool Contains(ChainWindow set, ChainWindow window) => (set & window) != 0;

    public static string NameOf(ChainWindow window)
    {
        for (var index = 0; index < Tracked.Length; index++)
        {
            if (Tracked[index].Window == window)
            {
                return Tracked[index].AddonName;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(window), window, "Only a single tracked window has an addon name.");
    }
}
