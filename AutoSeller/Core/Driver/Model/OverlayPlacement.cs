namespace AutoSeller.Core.Driver.Model;

internal readonly record struct ScreenRect(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;

    public float Bottom => Y + Height;

    public static ScreenRect OfAddon(float x, float y, float width, float height, float scale)
    {
        return new ScreenRect(x, y, width * scale, height * scale);
    }

    public ScreenRect Offset(float x, float y)
    {
        return this with { X = X + x, Y = Y + y };
    }
}

internal static class OverlayPlacement
{
    public static ScreenRect Beside(in ScreenRect window, float width, float height, float gap, in ScreenRect screen)
    {
        var x = window.Right + gap;
        var left = window.X - gap - width;
        if (x + width > screen.Right && left >= screen.X)
        {
            x = left;
        }

        return new ScreenRect(Clamp(x, screen.X, screen.Right - width), Clamp(window.Y, screen.Y, screen.Bottom - height), width, height);
    }

    public static ScreenRect Below(in ScreenRect window, float width, float height, float gap, in ScreenRect screen)
    {
        var y = window.Bottom + gap;
        var above = window.Y - gap - height;
        if (y + height > screen.Bottom && above >= screen.Y)
        {
            y = above;
        }

        return new ScreenRect(Clamp(window.Right - width, screen.X, screen.Right - width), Clamp(y, screen.Y, screen.Bottom - height), width, height);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return maximum < minimum ? minimum : Math.Clamp(value, minimum, maximum);
    }
}
