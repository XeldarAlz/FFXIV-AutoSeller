using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AutoSeller.Windows.Components;

internal struct TransientNotice
{
    private const int ShowMs = 2400;
    private const float FadeMs = 400f;

    private string? text;
    private Vector4 color;
    private long shownAtMs;
    private int showMs;

    public void Show(string message, Vector4 tint, int forMs = ShowMs)
    {
        text = message;
        color = tint;
        shownAtMs = Environment.TickCount64;
        showMs = forMs;
    }

    public void Clear() => text = null;

    public bool TryGet(out string message, out Vector4 tint)
    {
        var age = Environment.TickCount64 - shownAtMs;
        if (text is null || age > showMs)
        {
            text = null;
            message = string.Empty;
            tint = default;
            return false;
        }

        message = text;
        tint = Styling.WithAlpha(color, Math.Clamp((showMs - age) / FadeMs, 0f, 1f));
        return true;
    }

    public float DrawRight(float rightX, float midY, float maxWidth)
    {
        if (!TryGet(out var message, out var tint) || maxWidth <= 0f)
        {
            return 0f;
        }

        using (Fonts.PushCaption())
        {
            var shown = TextDraw.Truncate(message, maxWidth);
            var size = TextDraw.Measure(shown);
            TextDraw.At(shown, new Vector2(rightX - size.X, midY - size.Y * 0.5f), tint);
            return size.X;
        }
    }

    public void DrawLine(float width)
    {
        if (!TryGet(out var message, out var tint))
        {
            return;
        }

        Styling.VSpace(6f);
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(message, width), ImGui.GetCursorScreenPos(), tint);
            ImGui.Dummy(new Vector2(width, ImGui.GetTextLineHeight()));
        }
    }
}
