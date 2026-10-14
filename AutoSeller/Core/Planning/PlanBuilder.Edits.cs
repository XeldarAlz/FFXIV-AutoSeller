using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Characters.Model;
using AutoSeller.Core.Execution.Model;
using AutoSeller.Core.Planning.Model;

namespace AutoSeller.Core.Planning;

internal sealed partial class PlanBuilder
{
    public bool CanEdit => !runLive() && characters.Current is not null;

    public PlanEditRefusal SetExcluded(in PlanRow row, bool excluded)
    {
        if (!excluded)
        {
            return Remove(row, PlanEditKind.Exclude);
        }

        return Apply(row, PlanEdit.Exclude(row.Key, DateTime.UtcNow));
    }

    public PlanEditRefusal MoveTo(in PlanRow row, ulong retainerId)
    {
        return Apply(row, PlanEdit.Move(row.Key, retainerId, DateTime.UtcNow));
    }

    public PlanEditRefusal ClearMove(in PlanRow row)
    {
        return Remove(row, PlanEditKind.Move);
    }

    public PlanEditRefusal SetPrice(in PlanRow row, uint price)
    {
        return Apply(row, PlanEdit.Price(row.Key, price, DateTime.UtcNow));
    }

    public PlanEditRefusal ClearPrice(in PlanRow row)
    {
        return Remove(row, PlanEditKind.Price);
    }

    public bool Approve(in PlanRow row)
    {
        if (!CanEdit || !row.NeedsApproval)
        {
            return false;
        }

        var approval = ApprovalOf(row, row.Decision.ProposedPrice);
        characters.Current!.Approve(approval);
        RunLog.Info($"Plan: approved retainer {approval.RetainerId} slot {approval.Slot} item {approval.ItemId} dropping from {approval.OldPrice} to {approval.Price} gil.");
        return true;
    }

    public bool Withdraw(in PlanRow row)
    {
        if (!CanEdit || !row.IsListing)
        {
            return false;
        }

        var withdrawn = characters.Current!.Withdraw(ApprovalOf(row, 0));
        if (withdrawn)
        {
            RunLog.Info($"Plan: approval withdrawn for retainer {row.Key.RetainerId} slot {row.Key.Slot} item {row.Key.ItemId}.");
        }

        return withdrawn;
    }

    public bool SetRetainerEnabled(ulong retainerId, bool enabled)
    {
        if (!CanEdit || !characters.Current!.SetEnabled(retainerId, enabled))
        {
            return false;
        }

        RunLog.Info($"Plan: retainer {retainerId} {(enabled ? "takes part in" : "is left out of")} the next run.");
        return true;
    }

    private PlanEditRefusal Apply(in PlanRow row, in PlanEdit edit)
    {
        if (!CanEdit)
        {
            return PlanEditRefusal.RunInProgress;
        }

        var data = characters.Current!;
        var refusal = data.PlanEdits.TryApply(edit, row.EditTarget, out var updated);
        if (refusal != PlanEditRefusal.None)
        {
            RunLog.Info($"Plan: the {edit.Kind} edit of {row.Key} was refused ({refusal}).");
            return refusal;
        }

        data.SetPlanEdits(updated);
        RunLog.Info($"Plan: {edit.Kind} edit set on {row.Key} with value {edit.Value}.");
        return PlanEditRefusal.None;
    }

    private PlanEditRefusal Remove(in PlanRow row, PlanEditKind kind)
    {
        if (!CanEdit)
        {
            return PlanEditRefusal.RunInProgress;
        }

        var data = characters.Current!;
        data.SetPlanEdits(data.PlanEdits.Remove(row.Key, kind));
        RunLog.Info($"Plan: {kind} edit removed from {row.Key}.");
        return PlanEditRefusal.None;
    }

    private static ApprovedPrice ApprovalOf(in PlanRow row, uint price)
    {
        return ApprovedPrice.For(row.RetainerId, row.Listing, price);
    }
}
