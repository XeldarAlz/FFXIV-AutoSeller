using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Rules.Model;

namespace AutoSeller.Core.Tasks.Model;

internal readonly record struct RunLiveFacts(
    ulong CharacterContentId,
    uint CurrentWorldId,
    ulong OpenRetainerId,
    bool WindowChainReady,
    RuleBook Rules,
    PricingSettings Pricing,
    PlanEdits PlanEdits,
    bool JournalBlocksWrites,
    DateTime NowUtc);

internal readonly record struct GuardedRow(SaleWrite Write, SlotReading Planned, SlotReading Observed, bool Eligible, uint RuleFloor, uint VendorPrice);

internal static class RunGuard
{
    public static readonly TimeSpan DailyResetUtc = TimeSpan.FromHours(15);

    public static bool Applies(RunRowKind kind, in Decision decision)
    {
        return kind == RunRowKind.Listing
            ? decision.Action is SellAction.RepriceDown or SellAction.RepriceUp or SellAction.Return
            : decision.Action == SellAction.List;
    }

    public static SaleVerdict Evaluate(SellAuthorization authorization, in RunLiveFacts live, in GuardedRow row, CompetitorSnapshot competition, ValueBook values, Guid writeId)
    {
        var verdict = SaleGuard.Evaluate(new SaleGuardInput
        {
            Authorization = authorization,
            ActiveRunId = authorization.RunId,
            CharacterContentId = live.CharacterContentId,
            CurrentWorldId = live.CurrentWorldId,
            CurrentRules = live.Rules,
            CurrentPricing = live.Pricing,
            CurrentPlanEdits = live.PlanEdits,
            OpenRetainerId = live.OpenRetainerId,
            WindowChainReady = live.WindowChainReady,
            SearchGeneration = competition.Generation,
            SearchCapturedAtUtc = competition.CapturedAtUtc,
            NowUtc = live.NowUtc,
            Value = values.Snapshot(),
            JournalBlocksWrites = live.JournalBlocksWrites,
            Write = row.Write,
            Planned = row.Planned,
            Observed = row.Observed,
            EligibleForWrite = row.Eligible,
            RuleFloor = row.RuleFloor,
            VendorPrice = row.VendorPrice,
            VendorRuleMatches = false,
            IsGear = false,
            IsProtected = false,
            OnSellList = authorization.Rules.IsOnSellList(row.Write.ItemId),
        });
        if (verdict.IsAllowed && authorization.DryRun && values.TryReserve(writeId, row.Write) == ValueBookResult.Applied)
        {
            values.Confirm(writeId);
        }

        return verdict;
    }

    public static DriverHalt CheckBoundary(SellAuthorization authorization, in RunLiveFacts live)
    {
        if (live.CharacterContentId != authorization.CharacterContentId)
        {
            return DriverHalt.OtherCharacter;
        }

        if (live.CurrentWorldId != authorization.HomeWorldId)
        {
            return DriverHalt.LeftHomeWorld;
        }

        if (SaleGuard.CheckFrozenSettings(authorization, live.Rules, live.Pricing, live.PlanEdits) != SaleRejection.None)
        {
            return DriverHalt.SettingsChanged;
        }

        return DayOf(live.NowUtc) == DayOf(authorization.CreatedAtUtc) ? DriverHalt.None : DriverHalt.DayChanged;
    }

    public static int DayOf(DateTime utc)
    {
        return DateOnly.FromDateTime(utc - DailyResetUtc).DayNumber;
    }
}
