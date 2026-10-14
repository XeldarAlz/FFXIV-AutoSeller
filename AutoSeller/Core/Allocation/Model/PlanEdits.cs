using System.Collections.Immutable;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Allocation.Model;

internal sealed class PlanEdits
{
    public static readonly PlanEdits None = new([]);

    private PlanEdits(ImmutableArray<PlanEdit> items)
    {
        Items = items;
    }

    public ImmutableArray<PlanEdit> Items { get; }

    public static PlanEdits Restore(ReadOnlySpan<PlanEdit> edits)
    {
        var builder = ImmutableArray.CreateBuilder<PlanEdit>(edits.Length);
        for (var index = 0; index < edits.Length; index++)
        {
            if (!IsReplacedLater(edits, index))
            {
                builder.Add(edits[index]);
            }
        }

        return builder.Count == 0 ? None : new PlanEdits(builder.ToImmutable());
    }

    public static PlanEditRefusal Validate(in PlanEdit edit, in PlanEditTarget target)
    {
        return edit.Kind switch
        {
            PlanEditKind.Move => ValidateMove(edit, target),
            PlanEditKind.Price => ValidatePrice(edit, target),
            _ => PlanEditRefusal.None,
        };
    }

    public PlanEditRefusal TryApply(in PlanEdit edit, in PlanEditTarget target, out PlanEdits updated)
    {
        var refusal = Validate(edit, target);
        if (refusal != PlanEditRefusal.None)
        {
            updated = this;
            return refusal;
        }

        var builder = ImmutableArray.CreateBuilder<PlanEdit>(Items.Length + 1);
        for (var index = 0; index < Items.Length; index++)
        {
            var existing = Items[index];
            if (existing.Row != edit.Row || existing.Kind != edit.Kind)
            {
                builder.Add(existing);
            }
        }

        builder.Add(edit);
        updated = new PlanEdits(builder.ToImmutable());
        return PlanEditRefusal.None;
    }

    public PlanEdits Remove(PlanRowKey row, PlanEditKind kind)
    {
        var builder = ImmutableArray.CreateBuilder<PlanEdit>(Items.Length);
        for (var index = 0; index < Items.Length; index++)
        {
            var existing = Items[index];
            if (existing.Row != row || existing.Kind != kind)
            {
                builder.Add(existing);
            }
        }

        return builder.Count == Items.Length ? this : new PlanEdits(builder.ToImmutable());
    }

    public PlanEdits Refresh(ReadOnlySpan<PlanEditTarget> rows)
    {
        var builder = ImmutableArray.CreateBuilder<PlanEdit>(Items.Length);
        for (var index = 0; index < Items.Length; index++)
        {
            var edit = Items[index];
            if (TryFindRow(rows, edit.Row, out var target) && Validate(edit, target) == PlanEditRefusal.None)
            {
                builder.Add(edit);
            }
        }

        return builder.Count == Items.Length ? this : new PlanEdits(builder.ToImmutable());
    }

    public bool IsExcluded(PlanRowKey row)
    {
        return TryFind(row, PlanEditKind.Exclude, out _);
    }

    public bool TryGetMove(PlanRowKey row, out ulong retainerId)
    {
        var found = TryFind(row, PlanEditKind.Move, out var edit);
        retainerId = edit.Value;
        return found;
    }

    public bool TryGetPrice(PlanRowKey row, out uint price)
    {
        var found = TryFind(row, PlanEditKind.Price, out var edit);
        price = (uint)edit.Value;
        return found;
    }

    private static PlanEditRefusal ValidateMove(in PlanEdit edit, in PlanEditTarget target)
    {
        if (target.IsListing || target.BoundRetainerId != 0)
        {
            return PlanEditRefusal.NotMovable;
        }

        return edit.Value == 0 ? PlanEditRefusal.NoTargetRetainer : PlanEditRefusal.None;
    }

    private static PlanEditRefusal ValidatePrice(in PlanEdit edit, in PlanEditTarget target)
    {
        if (edit.Value is < MarketLimits.MinimumUnitPrice or > MarketLimits.MaximumUnitPrice)
        {
            return PlanEditRefusal.PriceOutOfRange;
        }

        return edit.Value < target.Floor ? PlanEditRefusal.PriceBelowFloor : PlanEditRefusal.None;
    }

    private static bool IsReplacedLater(ReadOnlySpan<PlanEdit> edits, int position)
    {
        var edit = edits[position];
        for (var index = position + 1; index < edits.Length; index++)
        {
            if (edits[index].Row == edit.Row && edits[index].Kind == edit.Kind)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindRow(ReadOnlySpan<PlanEditTarget> rows, PlanRowKey row, out PlanEditTarget target)
    {
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].Row == row)
            {
                target = rows[index];
                return true;
            }
        }

        target = default;
        return false;
    }

    private bool TryFind(PlanRowKey row, PlanEditKind kind, out PlanEdit edit)
    {
        for (var index = 0; index < Items.Length; index++)
        {
            var candidate = Items[index];
            if (candidate.Row == row && candidate.Kind == kind)
            {
                edit = candidate;
                return true;
            }
        }

        edit = default;
        return false;
    }
}
