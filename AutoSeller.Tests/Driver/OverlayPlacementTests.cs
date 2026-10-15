using AutoSeller.Core.Driver.Model;

namespace AutoSeller.Tests.Driver;

public sealed class OverlayPlacementTests
{
    private const float Gap = 8f;
    private static readonly ScreenRect screen = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void AnAddonRectangleGrowsWithItsScale(float scale)
    {
        var rect = ScreenRect.OfAddon(100, 200, 400, 300, scale);

        Assert.Equal(new ScreenRect(100, 200, 400 * scale, 300 * scale), rect);
        Assert.Equal(100 + 400 * scale, rect.Right);
        Assert.Equal(200 + 300 * scale, rect.Bottom);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void TheCardSitsRightOfTheWindowAtItsTopAtAnyScale(float scale)
    {
        var window = ScreenRect.OfAddon(300, 150, 360, 240, scale);

        var card = OverlayPlacement.Beside(window, 260, 140, Gap * scale, screen);

        Assert.Equal(window.Right + Gap * scale, card.X);
        Assert.Equal(window.Y, card.Y);
        Assert.Equal((260f, 140f), (card.Width, card.Height));
    }

    [Fact]
    public void TheCardMovesLeftWhenTheRightEdgeHasNoRoom()
    {
        var window = new ScreenRect(1500, 100, 360, 240);

        var card = OverlayPlacement.Beside(window, 260, 140, Gap, screen);

        Assert.Equal(1500 - Gap - 260, card.X);
    }

    [Fact]
    public void TheCardStaysOnTheScreenEvenWithNoRoomOnEitherSide()
    {
        var window = new ScreenRect(20, 1000, 1880, 240);

        var card = OverlayPlacement.Beside(window, 260, 140, Gap, screen);

        Assert.Equal(screen.Right - 260, card.X);
        Assert.Equal(screen.Bottom - 140, card.Y);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.75f)]
    public void TheButtonSitsUnderTheWindowAlignedToItsRightEdge(float scale)
    {
        var window = ScreenRect.OfAddon(400, 300, 500, 400, scale);

        var button = OverlayPlacement.Below(window, 180, 32, Gap, screen);

        Assert.Equal(window.Right - 180, button.X);
        Assert.Equal(window.Bottom + Gap, button.Y);
    }

    [Fact]
    public void TheButtonGoesAboveTheWindowWhenTheBottomHasNoRoom()
    {
        var window = new ScreenRect(400, 300, 500, 760);

        var button = OverlayPlacement.Below(window, 180, 32, Gap, screen);

        Assert.Equal(300 - Gap - 32, button.Y);
    }
}
