using AutoSeller.Core;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Inventory;
using AutoSeller.Core.Ledger;
using AutoSeller.Core.Localization;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Retainers.Model;
using AutoSeller.Core.Shared;
using AutoSeller.Windows.Components;
using Dalamud.Interface;
using ECommons.Throttlers;

namespace AutoSeller.Windows.Sections;

internal static class LedgerCards
{
    private const int FreshReadThrottleMs = 1_000;
    private const string FreshReadThrottleKey = "AutoSeller.Ledger.FreshRead";

    private static CachedText titleText;
    private static CachedText factsText;
    private static bool writeFailed;

    public static bool Shows(Plugin plugin)
    {
        if (plugin.Controller.Running)
        {
            return false;
        }

        var ledger = plugin.Ledger;
        return ledger.ReadError is not null || ledger.Review.IsDamaged || FirstUnresolved(ledger.Review, out _);
    }

    public static void Draw(Plugin plugin)
    {
        var ledger = plugin.Ledger;
        var review = ledger.Review;
        if (FirstUnresolved(review, out var unresolved))
        {
            DrawUnresolved(plugin, unresolved);
            return;
        }

        if (ledger.ReadError is not null || review.IsDamaged)
        {
            DrawDamaged(plugin, ledger.ReadError is not null);
        }
    }

    private static bool FirstUnresolved(JournalReview review, out UnresolvedWrite unresolved)
    {
        var writes = review.Unresolved;
        for (var index = 0; index < writes.Length; index++)
        {
            if (writes[index].HasIntent)
            {
                unresolved = writes[index];
                return true;
            }
        }

        unresolved = default;
        return false;
    }

    private static void DrawDamaged(Plugin plugin, bool unreadable)
    {
        var warning = writeFailed ? Loc.T(L.Journal.SetAsideFailed) : null;
        var card = ActionCard.Begin(Loc.T(L.Journal.DamagedTitle), Loc.T(unreadable ? L.Journal.UnreadableBody : L.Journal.DamagedBody), null, warning, Styling.AccentRose, unreadable ? 0 : 1);
        if (!unreadable && ActionCard.Button(ref card, "##asell_journal_aside", Loc.T(L.Journal.SetAside), Styling.AccentRose, PillButton.Emphasis.Tinted, true))
        {
            writeFailed = !plugin.Ledger.SetAside(DateTime.UtcNow, out var path);
            RunLog.Info(writeFailed ? $"Journal: setting the journal aside failed: {plugin.Ledger.Journal.LastError}." : $"Journal: the journal was set aside as {path}.");
        }

        ActionCard.End(card);
    }

    private static void DrawUnresolved(Plugin plugin, in UnresolvedWrite unresolved)
    {
        var intent = unresolved.Intent!;
        var write = intent.Write;
        var retainer = RetainerName(plugin, write.RetainerId);
        var latest = ReadAgainWhenOpen(plugin, write.RetainerId, intent.AtUtc);
        var hint = ReconcileHints.From(intent, latest, BagReader.HeldFor(write));
        var key = HashCode.Combine(plugin.Ledger.Revision, unresolved.WriteId, hint, Environment.TickCount64 / TimeUnits.MillisecondsPerMinute);
        if (!titleText.TryGet(key, out var title))
        {
            title = titleText.Set(key, Title(write));
        }

        if (!factsText.TryGet(key, out var facts))
        {
            facts = factsText.Set(key, string.Concat(Facts(plugin, intent, retainer), "\n", Hint(hint, retainer)));
        }

        var warning = writeFailed ? Loc.T(L.Journal.AnswerFailed) : null;
        var card = ActionCard.Begin(title, Loc.T(L.Journal.Body), facts, warning, Styling.AccentRose, 2, FontAwesomeIcon.QuestionCircle);
        if (ActionCard.Button(ref card, "##asell_journal_applied", Loc.T(L.Journal.WentThrough), Styling.AccentMint, hint == ReconcileHint.Applied ? PillButton.Emphasis.Filled : PillButton.Emphasis.Tinted, true))
        {
            Answer(plugin, unresolved, true, hint);
        }

        if (ActionCard.Button(ref card, "##asell_journal_not_applied", Loc.T(L.Journal.DidNotGoThrough), Styling.AccentRose, hint == ReconcileHint.NotApplied ? PillButton.Emphasis.Filled : PillButton.Emphasis.Tinted, true))
        {
            Answer(plugin, unresolved, false, hint);
        }

        ActionCard.End(card);
    }

    private static RetainerSnapshot? ReadAgainWhenOpen(Plugin plugin, ulong retainerId, DateTime intentAtUtc)
    {
        if (plugin.Characters.Current is not { } data)
        {
            return null;
        }

        data.TryGetSnapshot(retainerId, out var latest);
        if ((latest is null || latest.ReadAtUtc <= intentAtUtc)
            && plugin.Retainers.TryGetOpenRetainer(out var open)
            && open.RetainerId == retainerId
            && EzThrottler.Throttle(FreshReadThrottleKey, FreshReadThrottleMs))
        {
            var read = plugin.RetainerMarket.ReadOpenRetainer(DateTime.UtcNow);
            latest = read.Snapshot ?? latest;
        }

        return latest;
    }

    private static void Answer(Plugin plugin, in UnresolvedWrite unresolved, bool applied, ReconcileHint hint)
    {
        var agreesWithRead = (applied && hint == ReconcileHint.Applied) || (!applied && hint == ReconcileHint.NotApplied);
        var source = agreesWithRead ? ReconcileSource.FreshRead : ReconcileSource.Player;
        var answer = plugin.Ledger.Reconcile(unresolved.WriteId, applied, source, DateTime.UtcNow);
        writeFailed = answer == LedgerAnswer.WriteFailed;
        var write = unresolved.Intent!.Write;
        if (answer == LedgerAnswer.Applied && applied && write.Kind == WriteKind.Reprice)
        {
            plugin.Characters.Current?.RecordReprice(new RepriceKey(write.RetainerId, (byte)write.Slot, new ItemKey(write.ItemId, write.IsHq)), unresolved.Intent.AtUtc, write.OldPrice, write.NewPrice);
        }

        RunLog.Info($"Journal: write {unresolved.WriteId} answered as {(applied ? "went through" : "did not go through")} ({source}, the later read suggested {hint}): {answer}.");
    }

    private static string Facts(Plugin plugin, WriteIntent intent, string retainer)
    {
        var write = intent.Write;
        var name = PlanLabels.ItemName(plugin.GameItems, write.ItemId);
        var item = write.IsHq ? string.Concat(name, " ", Loc.T(L.Rules.HighQuality)) : name;
        var quantity = PlanLabels.Count(write.Quantity);
        var when = Formatting.RelativeTime(intent.AtUtc);
        return write.Kind switch
        {
            WriteKind.List or WriteKind.Return => Loc.T(L.Journal.ListFacts, item, quantity, retainer, when),
            WriteKind.Vendor => Loc.T(L.Journal.VendorFacts, item, quantity, retainer, when),
            _ => Loc.T(L.Journal.RepriceFacts, item, quantity, retainer, Formatting.Gil(write.OldPrice), Formatting.Gil(write.NewPrice), when),
        };
    }

    private static string Title(in SaleWrite write) => write.Kind switch
    {
        WriteKind.List => Loc.T(L.Journal.ListTitle, Formatting.Gil(write.NewPrice)),
        WriteKind.Return => Loc.T(L.Journal.ReturnTitle),
        WriteKind.Vendor => Loc.T(L.Journal.VendorTitle, Formatting.Gil(write.TryGetValue(out var value) ? value : 0)),
        _ => Loc.T(L.Journal.RepriceTitle, Formatting.Gil(write.NewPrice)),
    };

    private static string Hint(ReconcileHint hint, string retainer) => hint switch
    {
        ReconcileHint.Applied => Loc.T(L.Journal.HintApplied, retainer),
        ReconcileHint.NotApplied => Loc.T(L.Journal.HintNotApplied, retainer),
        ReconcileHint.Unclear => Loc.T(L.Journal.HintUnclear, retainer),
        _ => Loc.T(L.Journal.HintNoRead, retainer),
    };

    private static string RetainerName(Plugin plugin, ulong retainerId)
    {
        if (plugin.Characters.Current is { } data)
        {
            var retainers = data.Retainers;
            for (var index = 0; index < retainers.Count; index++)
            {
                if (retainers[index].RetainerId == retainerId && retainers[index].Name.Length > 0)
                {
                    return retainers[index].Name;
                }
            }
        }

        return Loc.T(L.Journal.UnknownRetainer);
    }
}
