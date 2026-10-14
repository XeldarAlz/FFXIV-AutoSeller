using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed class TotalsTiles
{
    private const int TileCount = 5;
    private const float TileGap = 8f;
    private const float MinimumTileWidth = 128f;

    private long textsGeneration = -1;
    private LanguageInfo? textsLanguage;
    private string changes = string.Empty;
    private string approvals = string.Empty;
    private string newListings = string.Empty;
    private string value = string.Empty;
    private string vendor = string.Empty;
    private string time = string.Empty;
    private string searches = string.Empty;

    public void Draw(SellPlan plan)
    {
        Refresh(plan);
        var scale = ImGuiHelpers.GlobalScale;
        var gap = TileGap * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var perRow = width >= TileCount * MinimumTileWidth * scale + gap * (TileCount - 1) ? TileCount
            : width >= 3f * MinimumTileWidth * scale + gap * 2f ? 3 : 2;
        var tileWidth = (width - gap * (perRow - 1)) / perRow;
        var awaiting = plan.Totals.AwaitingApproval > 0;

        StatTile.Draw(Loc.T(L.Plan.TileChanges), changes, awaiting ? approvals : null, awaiting ? Styling.AccentAmber : Styling.AccentBlue, tileWidth);
        Next(1, perRow, gap);
        StatTile.Draw(Loc.T(L.Plan.TileNew), newListings, null, Styling.AccentGlow, tileWidth);
        Next(2, perRow, gap);
        Tile(Loc.T(L.Plan.TileValue), value, null, Styling.AccentMint, tileWidth, L.Plan.TileValueHelp);
        Next(3, perRow, gap);
        Tile(Loc.T(L.Plan.TileVendor), vendor, null, Styling.AccentCoffee, tileWidth, L.Plan.TileVendorHelp);
        Next(4, perRow, gap);
        Tile(Loc.T(L.Plan.TileTime), time, searches, Styling.AccentNebula, tileWidth, L.Plan.TileTimeHelp);
    }

    private static void Tile(string label, string text, string? sub, Vector4 accent, float width, LocString help)
    {
        var origin = ImGui.GetCursorScreenPos();
        StatTile.Draw(label, text, sub, accent, width);
        if (Hit.HoveringRect(origin, origin + new Vector2(width, Layout.StatTileHeight * ImGuiHelpers.GlobalScale)))
        {
            Tooltip.Show(Loc.T(help));
        }
    }

    private static void Next(int index, int perRow, float gap)
    {
        if (index % perRow == 0)
        {
            return;
        }

        ImGui.SameLine(0f, gap);
    }

    private void Refresh(SellPlan plan)
    {
        if (plan.Generation == textsGeneration && ReferenceEquals(textsLanguage, Loc.Current))
        {
            return;
        }

        textsGeneration = plan.Generation;
        textsLanguage = Loc.Current;
        var totals = plan.Totals;
        changes = PlanLabels.Count(totals.ListingsToChange);
        approvals = Loc.Plural(L.Plan.TileApprovals, totals.AwaitingApproval);
        newListings = PlanLabels.Count(totals.NewListings);
        value = totals.ListedValue > 0 ? Formatting.Gil(totals.ListedValue) : PlanLabels.NoValue;
        vendor = totals.VendorGil > 0 ? Formatting.Gil(totals.VendorGil) : PlanLabels.NoValue;
        time = totals.RunTime > TimeSpan.Zero ? Formatting.Elapsed(totals.RunTime) : PlanLabels.NoValue;
        searches = Loc.Plural(L.Plan.TileSearches, totals.Searches);
    }
}
