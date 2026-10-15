using AutoSeller.Core.Game;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Globalization;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed class SellHeader
{
    private const float CardHeight = 62f;
    private const float PadX = 16f;
    private const float ChipGap = 8f;
    private const float ChipPadX = 10f;
    private const float ChipHeight = 26f;
    private const float RefreshSize = 26f;
    private const long AgeRefreshMs = 30_000;
    private const string RefreshId = "##asell_plan_refresh";
    private const string OnlyNeededId = "##asell_plan_only_needed";

    private TransientNotice notice;
    private HeaderKey key;
    private string name = string.Empty;
    private string world = string.Empty;
    private string retainers = string.Empty;
    private string listings = string.Empty;
    private string prices = string.Empty;
    private string watch = string.Empty;
    private string watchHelp = string.Empty;
    private string onlyNeededLabel = string.Empty;
    private string? onlyNeededCount;
    private string onlyNeededHelp = string.Empty;
    private bool undercut;
    private bool onlyNeeded;

    private readonly record struct HeaderKey(long Generation, LanguageInfo? Language, long Step, long ReferenceGeneration, bool Fetching, string CharacterName, uint HomeWorldId, bool OnlyNeeded, int WatchMinutes);

    public void Draw(Plugin plugin, SellPlan plan)
    {
        Refresh(plugin, plan);
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var end = origin + new Vector2(width, CardHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, Styling.AccentGlow, 0.04f);

        var padX = PadX * scale;
        var midY = origin.Y + CardHeight * scale * 0.5f;
        var right = DrawRefresh(plugin, end.X - padX, midY);
        right -= DrawChip(drawList, prices, plugin.References.Fetching ? Styling.AccentBlueSoft : Styling.TextDim, right, midY) + ChipGap * scale;
        var watchWidth = DrawChip(drawList, watch, undercut ? Styling.AccentRose : Styling.TextDim, right, midY, watchHelp);
        right -= watchWidth > 0f ? watchWidth + ChipGap * scale : 0f;
        right -= DrawOnlyNeeded(plugin, right, midY) + ChipGap * scale;
        right -= DrawChip(drawList, listings, Styling.TextDim, right, midY) + ChipGap * scale;
        right -= DrawChip(drawList, retainers, Styling.TextDim, right, midY) + ChipGap * scale;

        var textWidth = MathF.Max(0f, right - origin.X - padX);
        float titleHeight;
        using (Fonts.PushHeadline())
        {
            titleHeight = ImGui.GetTextLineHeight();
        }

        var top = midY - (titleHeight + TextDraw.CaptionLineHeight()) * 0.5f;
        using (Fonts.PushHeadline())
        {
            TextDraw.At(TextDraw.Truncate(name, textWidth), new Vector2(origin.X + padX, top), Styling.TextStrong);
        }

        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(world, textWidth), new Vector2(origin.X + padX, top + titleHeight), Styling.TextDim);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, CardHeight * scale));
        notice.DrawLine(width);
    }

    private float DrawRefresh(Plugin plugin, float right, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = RefreshSize * scale;
        var x = right - size;
        ImGui.SetCursorScreenPos(new Vector2(x, midY - size * 0.5f));
        var fetching = plugin.References.Fetching;
        if (IconButton.Draw(FontAwesomeIcon.SyncAlt, RefreshId, size, fetching ? Styling.AccentBlueSoft : Styling.TextSecondary, Loc.T(L.Plan.RefreshHelp), !fetching))
        {
            Report(plugin.Plan.Refresh(), plugin);
        }

        return x - ChipGap * scale;
    }

    private void Report(ReferenceRefresh refresh, Plugin plugin)
    {
        switch (refresh)
        {
            case ReferenceRefresh.Started:
                notice.Show(Loc.T(L.Plan.RefreshStarted), Styling.AccentMintSoft);
                return;
            case ReferenceRefresh.CoolingDown:
                var seconds = (int)Math.Ceiling(plugin.References.CooldownRemaining.TotalSeconds);
                notice.Show(Loc.T(L.Plan.RefreshCooling, seconds.ToString(CultureInfo.InvariantCulture)), Styling.AccentAmberSoft);
                return;
            case ReferenceRefresh.NoHomeWorld:
                notice.Show(Loc.T(L.Plan.RefreshNoWorld), Styling.AccentAmberSoft);
                return;
            default:
                notice.Show(Loc.T(L.Plan.RefreshNothing), Styling.TextDim);
                return;
        }
    }

    private float DrawOnlyNeeded(Plugin plugin, float right, float midY)
    {
        var height = ChipHeight * ImGuiHelpers.GlobalScale;
        using (Fonts.PushCaption())
        {
            var width = FilterChip.Width(onlyNeededLabel, onlyNeededCount);
            ImGui.SetCursorScreenPos(new Vector2(right - width, midY - height * 0.5f));
            if (FilterChip.Draw(OnlyNeededId, onlyNeededLabel, onlyNeededCount, Styling.AccentGlow, onlyNeeded, height, null, onlyNeededHelp))
            {
                plugin.Controller.SetOnlyRetainersThatNeedIt(!onlyNeeded);
            }

            return width;
        }
    }

    private static float DrawChip(ImDrawListPtr drawList, string label, Vector4 color, float right, float midY, string? tooltip = null)
    {
        if (label.Length == 0)
        {
            return 0f;
        }

        var scale = ImGuiHelpers.GlobalScale;
        using (Fonts.PushCaption())
        {
            var size = TextDraw.Measure(label);
            var width = size.X + ChipPadX * 2f * scale;
            var min = new Vector2(right - width, midY - ChipHeight * scale * 0.5f);
            var max = new Vector2(right, midY + ChipHeight * scale * 0.5f);
            Paint.Pill(drawList, min, max, Styling.WithAlpha(color, 0.12f), Styling.WithAlpha(color, 0.40f));
            TextDraw.At(label, new Vector2(min.X + ChipPadX * scale, midY - size.Y * 0.5f), Styling.Lighten(color, 0.2f));
            if (tooltip is not null && Hit.HoveringRect(min, max))
            {
                Tooltip.Show(tooltip);
            }

            return width;
        }
    }

    private void Refresh(Plugin plugin, SellPlan plan)
    {
        var data = plugin.Characters.Current;
        var characterName = data?.Name ?? string.Empty;
        var homeWorldId = plan.Input.HomeWorldId;
        var references = plugin.References;
        var configuration = plugin.Configuration;
        var next = new HeaderKey(plan.Generation, Loc.Current, Environment.TickCount64 / AgeRefreshMs, references.Latest?.Generation ?? 0, references.Fetching, characterName, homeWorldId, plugin.Controller.OnlyRetainersThatNeedIt, configuration.UndercutWatchMinutes);
        if (next == key)
        {
            return;
        }

        key = next;
        name = characterName;
        world = PlaceNames.World(homeWorldId);
        retainers = Loc.Plural(L.Plan.Retainers, plan.Retainers.Length);
        listings = Loc.T(L.Plan.ListingsInUse, PlanLabels.Count(plan.ListingsInUse), PlanLabels.Count(plan.ListingCapacity));
        var current = ReferenceSnapshot.ForHomeWorld(references.Latest, homeWorldId);
        prices = references.Fetching ? Loc.T(L.Plan.PricesFetching)
            : current is null ? Loc.T(L.Plan.PricesNone)
            : Loc.T(L.Plan.PricesAge, Formatting.RelativeTime(current.FetchedAtUtc));
        RefreshWatch(plan, configuration, next.OnlyNeeded);
    }

    private void RefreshWatch(SellPlan plan, Configuration configuration, bool onlyThoseThatNeedIt)
    {
        var undercutListings = 0;
        var rows = plan.Rows;
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].IsUndercut)
            {
                undercutListings++;
            }
        }

        undercut = undercutListings > 0;
        watch = undercut ? Loc.Plural(L.Plan.WatchUndercut, undercutListings) : string.Empty;
        watchHelp = configuration.UndercutWatchMinutes > WatchInterval.OffMinutes ? Loc.T(L.Plan.WatchHelp, configuration.UndercutWatchMinutes) : Loc.T(L.Plan.WatchOffHelp);
        onlyNeeded = onlyThoseThatNeedIt;
        onlyNeededLabel = Loc.T(L.Assist.OnlyNeeded);
        onlyNeededCount = onlyThoseThatNeedIt ? Loc.T(L.Plan.OnlyNeededCount, PlanLabels.Count(plan.RetainersToVisit(true).Length), PlanLabels.Count(plan.Input.EnabledRetainerIds().Length)) : null;
        onlyNeededHelp = Loc.T(L.Plan.OnlyNeededHelp);
    }
}
