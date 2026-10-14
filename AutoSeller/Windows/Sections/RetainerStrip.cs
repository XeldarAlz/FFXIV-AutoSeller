using System.Globalization;
using AutoSeller.Core.Game;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed class RetainerStrip
{
    private const float TileHeight = 74f;
    private const float MinimumTileWidth = 196f;
    private const float TileGap = 8f;
    private const float PadX = 12f;
    private const float PadY = 9f;
    private const float LineGap = 3f;
    private const long TextRefreshMs = 60_000;
    private const string ToggleIdPrefix = "##asell_strip_toggle_";

    private TileText[] texts = [];
    private StripKey key;

    private readonly record struct StripKey(long Generation, LanguageInfo? Language, long Step);

    private sealed class TileText
    {
        public string ToggleId = string.Empty;
        public string Name = string.Empty;
        public FittedText NameFit;
        public string Place = string.Empty;
        public FittedText PlaceFit;
        public string Gil = string.Empty;
        public string Visit = string.Empty;
        public FittedText VisitFit;
    }

    public void Draw(Plugin plugin, SellPlan plan)
    {
        Refresh(plan);
        var retainers = plan.Retainers;
        if (retainers.Length == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var gap = TileGap * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var perRow = Math.Max(1, (int)((width + gap) / (MinimumTileWidth * scale + gap)));
        perRow = Math.Min(perRow, retainers.Length);
        var tileWidth = (width - gap * (perRow - 1)) / perRow;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (retainers.Length + perRow - 1) / perRow;
        var editable = plugin.Plan.CanEdit;
        for (var index = 0; index < retainers.Length; index++)
        {
            var column = index % perRow;
            var row = index / perRow;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (TileHeight * scale + gap));
            DrawTile(plugin, retainers[index], texts[index], min, tileWidth, editable);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * TileHeight * scale + (rows - 1) * gap));
    }

    private static void DrawTile(Plugin plugin, in PlanRetainerSummary retainer, TileText text, Vector2 min, float width, bool editable)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var max = min + new Vector2(width, TileHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var accent = retainer.Enabled ? Styling.AccentGlow : Styling.TextMuted;
        Paint.Glass(drawList, min, max, Styling.CardRounding * scale, accent, retainer.Enabled ? 0.05f : 0.01f);

        var padX = PadX * scale;
        var toggleSize = ToggleSwitch.Size();
        var toggleOrigin = new Vector2(max.X - padX - toggleSize.X, min.Y + PadY * scale);
        ImGui.SetCursorScreenPos(toggleOrigin);
        var enabled = plugin.Characters.Current?.IsEnabled(retainer.RetainerId) ?? retainer.Enabled;
        if (editable)
        {
            if (ToggleSwitch.Draw(text.ToggleId, ref enabled))
            {
                plugin.Plan.SetRetainerEnabled(retainer.RetainerId, enabled);
            }
        }
        else
        {
            ToggleSwitch.DrawTrack(text.ToggleId, toggleOrigin, enabled, false);
        }

        if (Hit.HoveringRect(toggleOrigin, toggleOrigin + toggleSize))
        {
            Tooltip.Show(Loc.T(editable ? L.Plan.StripToggleHelp : L.Plan.StripLocked));
        }

        var textWidth = MathF.Max(0f, toggleOrigin.X - min.X - padX * 2f);
        var y = min.Y + PadY * scale;
        TextDraw.At(text.NameFit.Fit(text.Name, textWidth), new Vector2(min.X + padX, y), retainer.Enabled ? Styling.TextStrong : Styling.TextDim);
        y += ImGui.GetTextLineHeight() + LineGap * scale;
        using (Fonts.PushCaption())
        {
            var lineWidth = width - padX * 2f;
            TextDraw.At(text.PlaceFit.Fit(text.Place, lineWidth), new Vector2(min.X + padX, y), Styling.TextDim);
            y += ImGui.GetTextLineHeight() + LineGap * scale;
            var x = min.X + padX;
            if (retainer.GilNearCap)
            {
                var iconSize = TextDraw.IconSize(FontAwesomeIcon.ExclamationTriangle);
                TextDraw.Icon(FontAwesomeIcon.ExclamationTriangle, new Vector2(x, y + (ImGui.GetTextLineHeight() - iconSize.Y) * 0.5f), Styling.AccentAmber);
                x += iconSize.X + 4f * scale;
            }

            var gilColor = retainer.GilNearCap ? Styling.AccentAmberSoft : Styling.TextSecondary;
            TextDraw.At(text.Gil, new Vector2(x, y), gilColor);
            var gilEnd = x + TextDraw.Measure(text.Gil).X;
            if (retainer.GilNearCap && Hit.HoveringRect(new Vector2(min.X, y), new Vector2(gilEnd, y + ImGui.GetTextLineHeight())))
            {
                Tooltip.Show(Loc.T(L.Plan.StripGilNearCap));
            }

            var visitX = gilEnd + 10f * scale;
            TextDraw.At(text.VisitFit.Fit(text.Visit, MathF.Max(0f, max.X - padX - visitX)), new Vector2(visitX, y), Styling.TextMuted);
        }
    }

    private void Refresh(SellPlan plan)
    {
        var next = new StripKey(plan.Generation, Loc.Current, Environment.TickCount64 / TextRefreshMs);
        if (next == key && texts.Length == plan.Retainers.Length)
        {
            return;
        }

        key = next;
        var retainers = plan.Retainers;
        texts = new TileText[retainers.Length];
        for (var index = 0; index < retainers.Length; index++)
        {
            texts[index] = Build(retainers[index], index);
        }
    }

    private static TileText Build(in PlanRetainerSummary retainer, int index)
    {
        var listed = Loc.T(L.Plan.StripListed, retainer.Listings, RetainerSnapshot.SlotCount);
        var town = retainer.Visited ? PlaceNames.Town(retainer.Town) : string.Empty;
        var place = !retainer.Visited ? listed
            : town.Length > 0 ? string.Concat(town, TextDraw.Separator, listed)
            : string.Concat(Loc.T(L.Plan.StripNoMarket), TextDraw.Separator, listed);
        return new TileText
        {
            ToggleId = string.Concat(ToggleIdPrefix, index.ToString(CultureInfo.InvariantCulture)),
            Name = retainer.Name,
            Place = place,
            Gil = retainer.Visited ? Formatting.Gil(retainer.Gil) : PlanLabels.NoValue,
            Visit = retainer.Visited ? Loc.T(L.Plan.StripVisited, Formatting.RelativeTime(retainer.LastVisitUtc)) : Loc.T(L.Plan.StripNotVisited),
        };
    }
}
