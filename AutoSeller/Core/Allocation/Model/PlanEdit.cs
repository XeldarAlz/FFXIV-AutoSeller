namespace AutoSeller.Core.Allocation.Model;

internal enum PlanEditKind : byte
{
    Exclude,
    Move,
    Price,
}

internal enum PlanEditRefusal : byte
{
    None,
    PriceBelowFloor,
    PriceOutOfRange,
    NotMovable,
    NoTargetRetainer,
}

internal readonly record struct PlanEdit(PlanRowKey Row, PlanEditKind Kind, ulong Value, DateTime CreatedAtUtc)
{
    public static PlanEdit Exclude(PlanRowKey row, DateTime createdAtUtc)
    {
        return new PlanEdit(row, PlanEditKind.Exclude, 0, createdAtUtc);
    }

    public static PlanEdit Move(PlanRowKey row, ulong retainerId, DateTime createdAtUtc)
    {
        return new PlanEdit(row, PlanEditKind.Move, retainerId, createdAtUtc);
    }

    public static PlanEdit Price(PlanRowKey row, uint price, DateTime createdAtUtc)
    {
        return new PlanEdit(row, PlanEditKind.Price, price, createdAtUtc);
    }
}

internal readonly record struct PlanEditTarget(PlanRowKey Row, uint Floor, ulong BoundRetainerId, bool IsListing);
