using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Game.Ops;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AutoSeller.Windows.Overlays;

internal abstract unsafe class AddonOverlay : Window
{
    private const ImGuiWindowFlags OverlayFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.AlwaysAutoResize;

    private readonly string addonName;
    private ScreenRect addon;
    private Vector2 drawnSize;
    private IDisposable? font;
    private IDisposable? chrome;

    protected AddonOverlay(string windowName, string addonName)
        : base(windowName, OverlayFlags, true)
    {
        this.addonName = addonName;
        IsOpen = true;
        RespectCloseHotkey = false;
        ShowCloseButton = false;
        AllowPinning = false;
        AllowClickthrough = false;
        DisableWindowSounds = true;
        DisableFadeInFadeOut = true;
        PositionCondition = ImGuiCond.Always;
    }

    protected static float Scale => ImGuiHelpers.GlobalScale;

    public sealed override bool DrawConditions()
    {
        return Shows() && TryReadAddon(addonName, out addon);
    }

    public sealed override void PreDraw()
    {
        font = Fonts.PushBody();
        chrome = Styling.PushChrome(Vector2.Zero);
        var viewport = ImGuiHelpers.MainViewport.Size;
        var size = drawnSize == Vector2.Zero ? Estimate() : drawnSize;
        var placed = Place(addon, size, new ScreenRect(0, 0, viewport.X, viewport.Y));
        Position = new Vector2(placed.X, placed.Y);
    }

    public sealed override void Draw()
    {
        DrawOverlay();
        drawnSize = ImGui.GetWindowSize();
    }

    public sealed override void PostDraw()
    {
        chrome?.Dispose();
        chrome = null;
        font?.Dispose();
        font = null;
    }

    protected abstract bool Shows();

    protected abstract Vector2 Estimate();

    protected abstract ScreenRect Place(in ScreenRect window, Vector2 size, in ScreenRect screen);

    protected abstract void DrawOverlay();

    private static bool TryReadAddon(string name, out ScreenRect rect)
    {
        rect = default;
        if (!NpcInteraction.TryGetReadyAddon(name, out var unit) || !unit->IsVisible || unit->RootNode == null)
        {
            return false;
        }

        rect = ScreenRect.OfAddon(unit->X, unit->Y, unit->RootNode->Width, unit->RootNode->Height, unit->Scale);
        return true;
    }
}
