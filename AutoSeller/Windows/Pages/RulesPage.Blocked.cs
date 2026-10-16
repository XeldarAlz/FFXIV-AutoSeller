using AutoSeller.Core.Localization;
using AutoSeller.Core.Rules;
using AutoSeller.Core.Rules.Model;
using AutoSeller.Windows.Components;
using AutoSeller.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSeller.Windows.Pages;

internal sealed partial class RulesPage
{
    private const float BlockedReasonWidthShare = 0.42f;

    private string[] blockedCaptions = [];
    private string[] blockedReasons = [];
    private int blockedTextRevision = -1;
    private LanguageInfo? blockedTextLanguage;
    private int blockedRulesRevision = -1;
    private int blockedTabRevision = -1;
    private LanguageInfo? blockedTabLanguage;
    private BoardTab blockedTab;

    private void TrackBlockedTab(Plugin plugin, bool shown, bool reopened)
    {
        var scanner = plugin.BlockedScanner;
        var otherCharacter = scanner.Latest.Revision != 0 && scanner.Latest.ContentId != scanner.CurrentContentId;
        if (shown && (reopened || otherCharacter || !blockedShown || blockedRulesRevision != plugin.Configuration.RulesRevision))
        {
            blockedRulesRevision = plugin.Configuration.RulesRevision;
            scanner.Request();
        }

        blockedShown = shown;
    }

    private BoardTab BlockedTab(Plugin plugin)
    {
        var scan = plugin.BlockedScanner.Latest;
        if (scan.Revision != blockedTabRevision || !ReferenceEquals(blockedTabLanguage, Loc.Current))
        {
            blockedTabRevision = scan.Revision;
            blockedTabLanguage = Loc.Current;
            var count = scan.Revision == 0 ? PlanLabels.NoValue : PlanLabels.Count(scan.Rows.Length);
            blockedTab = new BoardTab(Loc.T(L.Rules.TabBlocked), count, Loc.T(L.Rules.BlockedStatus), Styling.TextDim);
        }

        return blockedTab;
    }

    private void DrawBlocked(Plugin plugin)
    {
        var scanner = plugin.BlockedScanner;
        var scan = scanner.Latest;
        if (scan.Revision == 0 || scan.ContentId != scanner.CurrentContentId)
        {
            EmptyState.Draw(Loc.T(L.Rules.BlockedReading));
            return;
        }

        if (!scan.LoggedIn)
        {
            EmptyState.Draw(Loc.T(L.Rules.BlockedLoggedOut));
            return;
        }

        if (scan.Rows.IsEmpty)
        {
            EmptyState.Draw(Loc.T(L.Rules.BlockedNone));
            return;
        }

        EnsureBlockedTexts(scan);
        for (var index = 0; index < scan.Rows.Length; index++)
        {
            DrawBlockedRow(scan.Rows[index], index);
        }
    }

    private static void DrawBlockedIntro(BlockedScanner scanner, in BoardBand band)
    {
        if (Board.Notice(band, "##asell_rules_blocked_refresh", FontAwesomeIcon.InfoCircle, Loc.T(L.Rules.BlockedIntro), Styling.AccentBlue, Styling.AccentBlueSoft, Loc.T(L.Rules.BlockedRefresh), PillButton.Emphasis.Tinted))
        {
            scanner.Request();
        }
    }

    private void DrawBlockedRow(in BlockedRow row, int index)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, ItemRowHeight * scale);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            Styling.VSpace(ItemRowGap);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Fill(drawList, origin, end, Styling.WithAlpha(Styling.Surface1, 0.45f), 8f * scale);
        var padX = RowPadX * scale;
        var reasonWidth = width * BlockedReasonWidthShare;
        var matchLabel = Loc.T(row.Item.Match == RuleMatch.SellList ? L.Rules.MatchSellList : L.Rules.MatchCategory);
        var midY = origin.Y + size.Y * 0.5f;
        var badgeWidth = Badge.Draw(drawList, matchLabel, row.Item.Match == RuleMatch.SellList ? Styling.AccentGlow : Styling.AccentBlue, end.X - padX, midY);
        var reasonRight = end.X - padX * 2f - badgeWidth;
        var reason = TextDraw.Truncate(blockedReasons[index], reasonWidth);
        var reasonSize = TextDraw.Measure(reason);
        TextDraw.At(reason, new Vector2(reasonRight - reasonSize.X, midY - reasonSize.Y * 0.5f), Styling.AccentAmberSoft);
        if (!ReferenceEquals(reason, blockedReasons[index]) && Hit.HoveringRect(new Vector2(reasonRight - reasonSize.X, origin.Y), new Vector2(reasonRight, end.Y)))
        {
            Tooltip.Show(blockedReasons[index]);
        }

        DrawItemIdentity(drawList, row.Name, row.IconId, blockedCaptions[index], origin, size.Y, reasonRight - reasonSize.X - padX - origin.X, row.Item.Slot.IsHq);
        ImGui.Dummy(size);
        Styling.VSpace(ItemRowGap);
    }

    private void EnsureBlockedTexts(BlockedScan scan)
    {
        if (blockedTextRevision == scan.Revision && ReferenceEquals(blockedTextLanguage, Loc.Current))
        {
            return;
        }

        blockedTextRevision = scan.Revision;
        blockedTextLanguage = Loc.Current;
        blockedCaptions = new string[scan.Rows.Length];
        blockedReasons = new string[scan.Rows.Length];
        for (var index = 0; index < scan.Rows.Length; index++)
        {
            var item = scan.Rows[index].Item;
            var quantity = item.Slot.Quantity.ToString("N0", Loc.Culture);
            blockedCaptions[index] = Loc.T(L.Rules.BlockedCaption, Loc.T(SourceLabel(item.Slot.Source)), quantity);
            blockedReasons[index] = Loc.T(BlockedLabel(item));
        }
    }

    private static LocString SourceLabel(SlotSource source) => source switch
    {
        SlotSource.Crystals => L.Rules.SourceCrystals,
        SlotSource.RetainerPage => L.Rules.SourceRetainer,
        SlotSource.Saddlebag => L.Rules.SourceSaddlebag,
        SlotSource.Armoury => L.Rules.SourceArmoury,
        _ => L.Rules.SourceBags,
    };

    private static LocString BlockedLabel(in BlockedItem item)
    {
        if (item.IsHeld)
        {
            return HoldLabel(item.Hold);
        }

        return item.WaitsForReference ? L.Rules.ReasonNoReference : ReasonLabel(item.Reason);
    }

    private static LocString HoldLabel(LotHold hold) => hold switch
    {
        LotHold.KeptInBags => L.Rules.HoldKeptInBags,
        LotHold.BelowLotSize => L.Rules.HoldBelowLotSize,
        LotHold.BagsFull => L.Rules.HoldBagsFull,
        _ => L.Rules.HoldListingsUp,
    };

    private static LocString ReasonLabel(EligibilityReason reason) => reason switch
    {
        EligibilityReason.SourceDisabled => L.Rules.ReasonSourceDisabled,
        EligibilityReason.Untradable => L.Rules.ReasonUntradable,
        EligibilityReason.NotMarketable => L.Rules.ReasonNotMarketable,
        EligibilityReason.Collectable => L.Rules.ReasonCollectable,
        EligibilityReason.Spiritbonded => L.Rules.ReasonSpiritbonded,
        EligibilityReason.InGearSet => L.Rules.ReasonInGearSet,
        EligibilityReason.ProtectedByAutoRetainer => L.Rules.ReasonProtected,
        EligibilityReason.NeverSell => L.Rules.ReasonNeverSell,
        EligibilityReason.RulePaused => L.Rules.ReasonRulePaused,
        EligibilityReason.BelowCategoryMinimum => L.Rules.ReasonBelowCategoryMinimum,
        EligibilityReason.QualityNotAllowed => L.Rules.ReasonQualityNotAllowed,
        EligibilityReason.UniqueAlreadyListed => L.Rules.ReasonUniqueListed,
        _ => L.Rules.ReasonNoLot,
    };
}
