using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Items;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Windows.Sections;

internal sealed class RunNotice
{
    private NoticeKey key;
    private string text = string.Empty;

    private readonly record struct NoticeKey(Guid RunId, int Revision, LanguageInfo? Language, bool Live);

    public string Live(GameItems items, in RunView run)
    {
        var next = new NoticeKey(run.RunId, run.Revision, Loc.Current, true);
        if (next != key)
        {
            key = next;
            text = LiveText(items, run);
        }

        return text;
    }

    public string Finished(in RunView run)
    {
        var next = new NoticeKey(run.RunId, run.Revision, Loc.Current, false);
        if (next != key)
        {
            key = next;
            text = FinishedText(run);
        }

        return text;
    }

    private static string LiveText(GameItems items, in RunView run)
    {
        var all = run.Retainers;
        var current = run.CurrentRetainer;
        var retainer = current >= 0 && current < all.Length
            ? string.Concat(Loc.T(L.Plan.RunRetainer, current + 1, all.Length), TextDraw.Separator, all[current].Name)
            : string.Empty;
        var row = run.CurrentRow;
        if (row == default(PlanRowKey))
        {
            return retainer;
        }

        var item = PlanLabels.ItemName(items, row.ItemId);
        if (!run.TryGetLatest(out var latest) || latest.Row != row)
        {
            return Join(Join(retainer, item), Loc.T(L.Plan.RunDeciding));
        }

        var price = latest.Decision.Evidence.CompetitorPrice;
        var competitor = price > 0 ? Loc.T(L.Plan.RunCompetitor, Formatting.Gil(price)) : string.Empty;
        return Join(Join(Join(retainer, item), competitor), Join(PlanLabels.Done(latest), PlanLabels.DoneReason(latest)));
    }

    private static string FinishedText(in RunView run)
    {
        var tally = run.Tally;
        return string.Concat(
            Loc.T(L.Plan.FinishedTitle), TextDraw.Separator,
            RunLabels.OutcomeLabel(run.State), TextDraw.Separator,
            Loc.T(L.History.Retainers, run.Visited, run.Retainers.Length), TextDraw.Separator,
            RunLabels.Changes(tally.Repriced, tally.Listed, tally.ListedValue),
            RunLabels.ReturnsAndVendor(tally.Returned, tally.Vendored, tally.VendoredValue), TextDraw.Separator,
            Formatting.Elapsed(run.EndedAtUtc - run.StartedAtUtc));
    }

    private static string Join(string first, string second)
    {
        if (first.Length == 0)
        {
            return second;
        }

        return second.Length == 0 ? first : string.Concat(first, TextDraw.Separator, second);
    }
}
