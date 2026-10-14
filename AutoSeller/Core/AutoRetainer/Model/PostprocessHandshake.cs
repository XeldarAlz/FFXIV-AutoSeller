namespace AutoSeller.Core.AutoRetainer.Model;

internal enum HandshakePhase : byte { Idle, Requested, Working, Unwinding, Returning }

internal enum ReadyStep : byte { Start, HandBack }

internal enum HandshakeStep : byte { Wait, StopJob, EndJob, Abandon, Finish }

internal readonly record struct HandshakeFacts(bool SameCharacter, bool JobRunning, bool WindowsSettling, bool AtRetainerMenu, long NowMs);

internal sealed class PostprocessHandshake
{
    public const long UnwindGraceMs = 60_000;
    public const long ReturnBoundMs = 45_000;

    private long startedAtMs;
    private long phaseSinceMs;
    private PostprocessBudget budget;
    private bool endIssued;

    public HandshakePhase Phase { get; private set; } = HandshakePhase.Idle;

    public string RetainerName { get; private set; } = string.Empty;

    public ulong RetainerId { get; private set; }

    public int PlannedWork { get; private set; }

    public Guid JobId { get; private set; }

    public bool SellNowArmed { get; private set; }

    public bool StoodDown { get; private set; }

    public bool OwesFinish => Phase is HandshakePhase.Working or HandshakePhase.Unwinding or HandshakePhase.Returning;

    public TimeSpan Allowance => budget.Allowance;

    public void ArmSellNow()
    {
        SellNowArmed = true;
        StoodDown = false;
    }

    public void StandDown()
    {
        StoodDown = true;
        SellNowArmed = false;
    }

    public void LeftBell()
    {
        SellNowArmed = false;
        StoodDown = false;
        if (Phase == HandshakePhase.Requested)
        {
            Clear();
        }
    }

    public bool Request(string retainerName, ulong retainerId, int plannedWork)
    {
        if (OwesFinish || retainerId == 0)
        {
            return false;
        }

        Phase = HandshakePhase.Requested;
        RetainerName = retainerName;
        RetainerId = retainerId;
        PlannedWork = plannedWork;
        return true;
    }

    public void RequestRefused()
    {
        if (Phase == HandshakePhase.Requested)
        {
            Clear();
        }
    }

    public ReadyStep Ready(string retainerName, long nowMs)
    {
        if (Phase != HandshakePhase.Requested || !string.Equals(retainerName, RetainerName, StringComparison.Ordinal))
        {
            if (!OwesFinish)
            {
                Clear();
            }

            return ReadyStep.HandBack;
        }

        budget = PostprocessBudget.For(PlannedWork);
        startedAtMs = nowMs;
        Enter(HandshakePhase.Working, nowMs);
        return ReadyStep.Start;
    }

    public void JobStarted(Guid jobId)
    {
        if (Phase == HandshakePhase.Working)
        {
            JobId = jobId;
        }
    }

    public HandshakeStep Tick(in HandshakeFacts facts)
    {
        if (!OwesFinish)
        {
            return HandshakeStep.Wait;
        }

        if (!facts.SameCharacter)
        {
            return HandshakeStep.Abandon;
        }

        switch (Phase)
        {
            case HandshakePhase.Working:
                if (!facts.JobRunning)
                {
                    Enter(HandshakePhase.Returning, facts.NowMs);
                    return Return(facts);
                }

                if (budget.Judge(TimeSpan.FromMilliseconds(facts.NowMs - startedAtMs)) == BudgetVerdict.Unwind)
                {
                    Enter(HandshakePhase.Unwinding, facts.NowMs);
                    return HandshakeStep.StopJob;
                }

                return HandshakeStep.Wait;
            case HandshakePhase.Unwinding:
                if (!facts.JobRunning)
                {
                    Enter(HandshakePhase.Returning, facts.NowMs);
                    return Return(facts);
                }

                if (!endIssued && facts.NowMs - phaseSinceMs >= UnwindGraceMs)
                {
                    endIssued = true;
                    return HandshakeStep.EndJob;
                }

                return HandshakeStep.Wait;
            default:
                return Return(facts);
        }
    }

    public bool Finish()
    {
        var owed = OwesFinish;
        Clear();
        return owed;
    }

    private HandshakeStep Return(in HandshakeFacts facts)
    {
        if (facts.NowMs - phaseSinceMs >= ReturnBoundMs)
        {
            return HandshakeStep.Finish;
        }

        return !facts.WindowsSettling && facts.AtRetainerMenu ? HandshakeStep.Finish : HandshakeStep.Wait;
    }

    private void Enter(HandshakePhase phase, long nowMs)
    {
        Phase = phase;
        phaseSinceMs = nowMs;
    }

    private void Clear()
    {
        Phase = HandshakePhase.Idle;
        RetainerName = string.Empty;
        RetainerId = 0;
        PlannedWork = 0;
        JobId = Guid.Empty;
        endIssued = false;
        budget = default;
    }
}
