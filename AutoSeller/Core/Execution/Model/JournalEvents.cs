using System.Collections.Immutable;
using System.Text.Json.Serialization;
using AutoSeller.Core.Allocation.Model;
using AutoSeller.Core.Pricing.Model;

namespace AutoSeller.Core.Execution.Model;

internal enum WriteRejection : byte
{
    Unchanged,
    WindowMismatch,
    Cancelled,
    PromptRefused,
}

internal enum ReconcileSource : byte
{
    Player,
    FreshRead,
}

internal sealed record RunAuthorized : JournalEvent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.RunAuthorized;

    public required ulong CharacterContentId { get; init; }

    public required uint HomeWorldId { get; init; }

    public required ImmutableArray<ulong> RetainerIds { get; init; }

    public required AuthorizedRules Rules { get; init; }

    public required PricingSettings Pricing { get; init; }

    public required ImmutableArray<PlanEdit> PlanEdits { get; init; }

    public required RunCaps Caps { get; init; }

    public required bool DryRun { get; init; }

    public required int PolicyVersion { get; init; }

    public required ImmutableArray<ApprovedPrice> ApprovedPrices { get; init; }

    public required DateTime AuthorizedAtUtc { get; init; }

    public override bool IsWellFormed()
    {
        return base.IsWellFormed()
            && CharacterContentId != 0
            && !RetainerIds.IsDefaultOrEmpty
            && Rules is not null
            && Rules.IsWellFormed()
            && Pricing is not null
            && !PlanEdits.IsDefault
            && !ApprovedPrices.IsDefault;
    }
}

internal abstract record WriteIntent : WriteEvent
{
    public const long Unread = -1;

    [JsonIgnore]
    public abstract WriteKind WriteKind { get; }

    public required SaleWrite Write { get; init; }

    public long ItemCountBefore { get; init; } = Unread;

    public long GilBefore { get; init; } = Unread;

    public static WriteIntent Record(in SaleWrite write, long sequence, Guid runId, DateTime atUtc, Guid writeId)
    {
        return write.Kind switch
        {
            WriteKind.List => new ListIntent { Sequence = sequence, RunId = runId, AtUtc = atUtc, WriteId = writeId, Write = write },
            WriteKind.Reprice => new RepriceIntent { Sequence = sequence, RunId = runId, AtUtc = atUtc, WriteId = writeId, Write = write },
            WriteKind.Return => new ReturnIntent { Sequence = sequence, RunId = runId, AtUtc = atUtc, WriteId = writeId, Write = write },
            WriteKind.Vendor => new VendorIntent { Sequence = sequence, RunId = runId, AtUtc = atUtc, WriteId = writeId, Write = write },
            WriteKind.Move => new MoveIntent { Sequence = sequence, RunId = runId, AtUtc = atUtc, WriteId = writeId, Write = write },
            _ => throw new ArgumentOutOfRangeException(nameof(write), write.Kind, null),
        };
    }

    public override bool IsWellFormed()
    {
        return base.IsWellFormed() && Write.Kind == WriteKind && Write.IsWellFormed;
    }
}

internal sealed record ListIntent : WriteIntent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.ListIntent;

    [JsonIgnore]
    public override WriteKind WriteKind => WriteKind.List;
}

internal sealed record RepriceIntent : WriteIntent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.RepriceIntent;

    [JsonIgnore]
    public override WriteKind WriteKind => WriteKind.Reprice;
}

internal sealed record ReturnIntent : WriteIntent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.ReturnIntent;

    [JsonIgnore]
    public override WriteKind WriteKind => WriteKind.Return;
}

internal sealed record VendorIntent : WriteIntent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.VendorIntent;

    [JsonIgnore]
    public override WriteKind WriteKind => WriteKind.Vendor;
}

internal sealed record MoveIntent : WriteIntent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.MoveIntent;

    [JsonIgnore]
    public override WriteKind WriteKind => WriteKind.Move;
}

internal sealed record WriteConfirmed : WriteEvent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.WriteConfirmed;

    public required uint Quantity { get; init; }

    public required uint UnitPrice { get; init; }

    public override bool IsWellFormed()
    {
        return base.IsWellFormed() && Quantity > 0;
    }
}

internal sealed record WriteRejected : WriteEvent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.WriteRejected;

    public required WriteRejection Reason { get; init; }
}

internal sealed record WriteUnknown : WriteEvent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.WriteUnknown;

    public required ReadBackCause Cause { get; init; }
}

internal sealed record Reconciled : WriteEvent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.Reconciled;

    public required bool Applied { get; init; }

    public required ReconcileSource Source { get; init; }
}

internal sealed record RunEnded : JournalEvent
{
    [JsonIgnore]
    public override string Kind => JournalEventKind.RunEnded;

    public required RunState FinalState { get; init; }

    public override bool IsWellFormed()
    {
        return base.IsWellFormed() && FinalState.IsFinal();
    }
}
