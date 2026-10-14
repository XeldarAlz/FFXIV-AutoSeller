using System.Globalization;
using System.Text;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Planning.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal sealed partial class PlanTable
{
    private const int MaximumBadges = 5;

    private readonly StringBuilder tooltip = new();
    private readonly string[] badgeLabels = new string[MaximumBadges];
    private readonly Vector4[] badgeColors = new Vector4[MaximumBadges];

    private void Refresh(Plugin plugin, SellPlan plan, in RunView run, bool showRun)
    {
        var key = new TextKey(plan.Generation, run.RunId, showRun ? run.Decisions.Length : 0, showRun, Loc.Current, Environment.TickCount64 / TextRefreshMs, plugin.Plan.CanEdit);
        if (key == textKey)
        {
            return;
        }

        textKey = key;
        Decided(run, showRun);
        var rows = plan.Rows;
        if (texts.Length != rows.Length)
        {
            texts = new RowText[rows.Length];
        }

        for (var index = 0; index < rows.Length; index++)
        {
            texts[index] = Build(plugin.GameItems, rows[index], run, index, key.Editable);
        }

        var groups = plan.Groups;
        if (groupTexts.Length != groups.Length)
        {
            groupTexts = new GroupText[groups.Length];
        }

        for (var index = 0; index < groups.Length; index++)
        {
            groupTexts[index] = BuildGroup(plan, groups[index]);
        }

        Layout(plan);
    }

    private void Decided(in RunView run, bool showRun)
    {
        decided.Clear();
        if (!showRun)
        {
            return;
        }

        var decisions = run.Decisions;
        for (var index = 0; index < decisions.Length; index++)
        {
            decided[decisions[index].Row] = index;
        }
    }

    private RowText Build(GameItems items, in PlanRow row, in RunView run, int index, bool editable)
    {
        var item = items.Get(row.Key.ItemId);
        var suffix = index.ToString(CultureInfo.InvariantCulture);
        var muted = row.Has(PlanRowEdits.Excluded) || row.Decision.Action == SellAction.Skip || row.Duty == ListingDuty.NotManaged;
        var text = new RowText
        {
            Id = string.Concat(RowIdPrefix, suffix),
            MenuId = string.Concat(MenuButtonPrefix, suffix),
            ApproveId = string.Concat(ApproveButtonPrefix, suffix),
            Name = PlanLabels.ItemName(items, row.Key.ItemId),
            IconId = item.IconId,
            HighQuality = row.Key.IsHq,
            Source = PlanLabels.Source(row),
            Quantity = PlanLabels.Count(row.Quantity),
            Current = row.IsListing ? PlanLabels.Gil(row.CurrentPrice) : PlanLabels.NoValue,
            Suggested = SuggestedOf(row),
            Reference = PlanLabels.Gil(row.Reference.AverageSalePrice),
            Action = PlanLabels.Action(row.Decision.Action),
            ActionColor = PlanLabels.ActionColor(row.Decision.Action),
            Caption = PlanLabels.RowReason(row),
            CaptionColor = row.NeedsApproval ? Styling.AccentAmberSoft : muted ? Styling.TextMuted : Styling.TextDim,
            Muted = muted,
            Approvable = row.NeedsApproval,
            ApproveTooltip = row.NeedsApproval ? Loc.T(L.Plan.ApproveHelp, PlanLabels.Gil(row.CurrentPrice), PlanLabels.Gil(row.Decision.ProposedPrice)) : string.Empty,
        };

        var overlaid = decided.TryGetValue(row.Key, out var decisionIndex);
        if (overlaid)
        {
            var decision = run.Decisions[decisionIndex];
            text.Action = PlanLabels.Done(decision);
            text.ActionColor = PlanLabels.DoneColor(decision);
            text.Caption = PlanLabels.DoneReason(decision);
            text.CaptionColor = Styling.AccentBlueSoft;
            text.Suggested = decision.WouldReprice || decision.WouldList ? PlanLabels.Gil(decision.Decision.Price) : text.Suggested;
        }

        BadgesOf(row, text);
        text.Tooltip = TooltipOf(row, text, overlaid ? run.Decisions[decisionIndex] : null, editable);
        return text;
    }

    private static string SuggestedOf(in PlanRow row)
    {
        var decision = row.Decision;
        if (row.Writes)
        {
            return PlanLabels.Gil(decision.Price);
        }

        return decision.ProposedPrice > 0 && decision.ProposedPrice != row.CurrentPrice ? PlanLabels.Gil(decision.ProposedPrice) : PlanLabels.NoValue;
    }

    private void BadgesOf(in PlanRow row, RowText text)
    {
        var count = 0;
        if (row.Key.IsHq)
        {
            Add(ref count, Loc.T(L.Rules.HighQuality), Styling.AccentAmber);
        }

        if (row.Has(PlanRowEdits.Excluded))
        {
            Add(ref count, Loc.T(L.Plan.BadgeExcluded), Styling.TextMuted);
        }

        if (row.Has(PlanRowEdits.Moved))
        {
            Add(ref count, Loc.T(L.Plan.BadgeMoved), Styling.AccentBlue);
        }

        if (row.Has(PlanRowEdits.PriceSet))
        {
            Add(ref count, Loc.T(L.Plan.BadgePriceSet), Styling.AccentGlow);
        }

        if (row.Has(PlanRowEdits.Approved) && row.Decision.Reason == DecisionReason.Approved)
        {
            Add(ref count, Loc.T(L.Plan.BadgeApproved), Styling.AccentMint);
        }

        text.Badges = count == 0 ? [] : badgeLabels.AsSpan(0, count).ToArray();
        text.BadgeColors = count == 0 ? [] : badgeColors.AsSpan(0, count).ToArray();
    }

    private void Add(ref int count, string label, Vector4 color)
    {
        badgeLabels[count] = label;
        badgeColors[count] = color;
        count++;
    }

    private string TooltipOf(in PlanRow row, RowText text, RunDecision? decision, bool editable)
    {
        tooltip.Clear().Append(text.Name).Append('\n').Append(text.Caption);
        var evidence = decision?.Decision.Evidence ?? row.Decision.Evidence;
        if (evidence.Floor > 0)
        {
            tooltip.Append('\n').Append(Loc.T(L.Plan.TipFloor, PlanLabels.Gil(evidence.Floor)));
        }

        if (evidence.CompetitorPrice > 0)
        {
            tooltip.Append('\n').Append(Loc.T(L.Plan.TipCompetitor, PlanLabels.Gil(evidence.CompetitorPrice)));
        }

        var proposed = decision?.Decision.ProposedPrice ?? row.Decision.ProposedPrice;
        if (proposed > 0 && proposed != row.CurrentPrice && !row.Writes)
        {
            tooltip.Append('\n').Append(Loc.T(L.Plan.TipProposed, PlanLabels.Gil(proposed)));
        }

        if (row.Reference.AverageSalePrice > 0)
        {
            tooltip.Append('\n').Append(Loc.T(L.Plan.TipReference, PlanLabels.Gil(row.Reference.AverageSalePrice), PlanLabels.ReferenceFrom(row.Reference.Source)));
        }

        tooltip.Append("\n\n").Append(decision is null ? Loc.T(L.Plan.ColumnSuggestedHelp) : Loc.T(L.Plan.DoneHelp));
        if (editable)
        {
            tooltip.Append('\n').Append(Loc.T(L.Plan.TipEdit));
        }

        return tooltip.ToString();
    }

    private static GroupText BuildGroup(SellPlan plan, in PlanGroup group)
    {
        if (group.RetainerId == PlanRow.Unplaced)
        {
            return new GroupText { Title = Loc.T(L.Plan.GroupUnplaced), Caption = Loc.Plural(L.Plan.GroupRows, group.Count) };
        }

        plan.TryGetRetainer(group.RetainerId, out var retainer);
        if (group.Count == 0)
        {
            return new GroupText { Title = retainer.Name, Caption = Loc.T(retainer.Visited ? L.Plan.GroupNothing : L.Plan.GroupNotVisited) };
        }

        var caption = group.Changes > 0
            ? string.Concat(Loc.Plural(L.Plan.GroupRows, group.Count), TextDraw.Separator, Loc.T(L.Plan.GroupChanges, group.Changes))
            : Loc.Plural(L.Plan.GroupRows, group.Count);
        return new GroupText { Title = retainer.Name, Caption = caption };
    }

    private void Layout(SellPlan plan)
    {
        var groups = plan.Groups;
        var needed = groups.Length + plan.Rows.Length;
        if (entries.Length < needed)
        {
            entries = new Entry[needed];
            tops = new float[needed];
        }

        entryCount = 0;
        var top = 0f;
        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var group = groups[groupIndex];
            entries[entryCount] = new Entry(true, groupIndex);
            tops[entryCount] = top;
            entryCount++;
            top += HeaderHeight + RowGap;
            for (var rowIndex = group.Start; rowIndex < group.Start + group.Count; rowIndex++)
            {
                entries[entryCount] = new Entry(false, rowIndex);
                tops[entryCount] = top;
                entryCount++;
                top += RowHeight + RowGap;
            }
        }

        totalHeight = MathF.Max(0f, top - RowGap);
    }
}
