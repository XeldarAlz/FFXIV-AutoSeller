using AutoSeller.Core.Driver.Model;

namespace AutoSeller.Tests.Run;

public sealed class ChainWindowTests
{
    [Fact]
    public void EveryWindowIsTrackedOnceUnderItsAddonName()
    {
        var seen = ChainWindow.None;
        for (var index = 0; index < ChainWindows.Tracked.Length; index++)
        {
            var tracked = ChainWindows.Tracked[index];
            Assert.False(ChainWindows.Contains(seen, tracked.Window));
            Assert.Equal(Enum.GetName(tracked.Window), tracked.AddonName);
            seen |= tracked.Window;
        }

        var windows = Enum.GetValues<ChainWindow>();
        var every = ChainWindow.None;
        for (var index = 0; index < windows.Length; index++)
        {
            every |= windows[index];
        }

        Assert.Equal(every, seen);
    }

    [Theory]
    [InlineData((uint)ChainWindow.RetainerSellList, "RetainerSellList")]
    [InlineData((uint)ChainWindow.SelectYesno, "SelectYesno")]
    [InlineData((uint)ChainWindow.InputNumeric, "InputNumeric")]
    [InlineData((uint)ChainWindow.ItemSearchResult, "ItemSearchResult")]
    public void TheGameWindowsKeepTheirAddonNames(uint window, string addonName)
    {
        var found = string.Empty;
        for (var index = 0; index < ChainWindows.Tracked.Length; index++)
        {
            if (ChainWindows.Tracked[index].Window == (ChainWindow)window)
            {
                found = ChainWindows.Tracked[index].AddonName;
            }
        }

        Assert.Equal(addonName, found);
    }

    [Fact]
    public void UnwindClosesTheTopmostWindowFirstDownToTheRetainerMenu()
    {
        var visible = ChainWindow.SelectString | ChainWindow.RetainerSellList | ChainWindow.RetainerSell | ChainWindow.ItemSearchResult | ChainWindow.InputNumeric;
        ChainWindow[] expected = [ChainWindow.InputNumeric, ChainWindow.ItemSearchResult, ChainWindow.RetainerSell, ChainWindow.RetainerSellList, ChainWindow.None];

        for (var index = 0; index < expected.Length; index++)
        {
            var next = ChainUnwind.NextToClose(visible);
            Assert.Equal(expected[index], next);
            visible &= ~next;
        }

        Assert.Equal(ChainWindow.SelectString, visible);
    }

    [Fact]
    public void UnwindClosesAContextMenuAndTheSaleHistory()
    {
        Assert.Equal(ChainWindow.ContextMenu, ChainUnwind.NextToClose(ChainWindow.RetainerSellList | ChainWindow.ContextMenu));
        Assert.Equal(ChainWindow.RetainerHistory, ChainUnwind.NextToClose(ChainWindow.SelectString | ChainWindow.RetainerHistory));
    }

    [Fact]
    public void UnwindNeverTouchesAPromptOrTheRetainerMenu()
    {
        Assert.Equal(ChainWindow.None, ChainUnwind.NextToClose(ChainWindow.SelectYesno | ChainWindow.SelectString | ChainWindow.Talk | ChainWindow.RetainerList | ChainWindow.SelectOk));
    }
}
