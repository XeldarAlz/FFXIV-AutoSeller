namespace AutoSeller.Core.Execution.Model;

internal enum RunWrite : byte
{
    None,
    Confirmed = 2,
    Rejected,
    Unknown,
}

internal readonly record struct WriteSettlement(RunWrite Write, RunReaction Reaction, WriteRejection Rejection, ReadBackCause Cause)
{
    public static WriteSettlement Of(in ReadBackVerdict verdict, bool promptRefused)
    {
        return verdict.Outcome switch
        {
            ReadBackOutcome.Confirmed => new WriteSettlement(
                RunWrite.Confirmed,
                promptRefused ? RunReactions.ForRejection(WriteRejection.PromptRefused) : RunReactions.ForReadBack(verdict.Outcome),
                default,
                ReadBackCause.None),
            ReadBackOutcome.Rejected => NotConfirmed(promptRefused ? WriteRejection.PromptRefused : WriteRejection.Unchanged),
            ReadBackOutcome.Unknown => new WriteSettlement(RunWrite.Unknown, RunReactions.ForReadBack(verdict.Outcome), default, verdict.Cause),
            _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict.Outcome, "A pending read-back settles nothing yet."),
        };
    }

    public static WriteSettlement NotConfirmed(WriteRejection reason)
    {
        return new WriteSettlement(RunWrite.Rejected, RunReactions.ForRejection(reason), reason, ReadBackCause.None);
    }

    public WriteEvent ToJournalEvent(in SaleWrite write, Guid writeId, Guid runId, long sequence, DateTime atUtc)
    {
        return Write switch
        {
            RunWrite.Confirmed => new WriteConfirmed { Sequence = sequence, RunId = runId, AtUtc = atUtc, WriteId = writeId, Quantity = write.Quantity, UnitPrice = write.NewPrice },
            RunWrite.Rejected => new WriteRejected { Sequence = sequence, RunId = runId, AtUtc = atUtc, WriteId = writeId, Reason = Rejection },
            RunWrite.Unknown => new WriteUnknown { Sequence = sequence, RunId = runId, AtUtc = atUtc, WriteId = writeId, Cause = Cause },
            _ => throw new InvalidOperationException($"A {Write} write has no outcome to journal."),
        };
    }

    public ValueBookResult ApplyTo(ValueBook values, Guid writeId)
    {
        return Write switch
        {
            RunWrite.Confirmed => values.Confirm(writeId),
            RunWrite.Rejected => values.Release(writeId),
            _ => ValueBookResult.WritePending,
        };
    }
}
