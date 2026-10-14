using AutoSeller.Core.Tasks.Model;
using System.Numerics;

namespace AutoSeller.Core.Driver.Model;

internal enum ChainExpectation : byte { Opens, Closes }

internal enum ChainAction : byte { Wait, Next, Retry, Unwind, Halt, Pause }

internal readonly record struct ChainStep(ChainWindow Window, ChainExpectation Expectation, int TimeoutMs, ChainWindow Tolerated, uint PromptRow)
{
    public const uint NoPrompt = 0;

    public static ChainStep Opens(ChainWindow window, int timeoutMs, ChainWindow tolerated = ChainWindow.None)
        => new(window, ChainExpectation.Opens, timeoutMs, tolerated, NoPrompt);

    public static ChainStep Closes(ChainWindow window, int timeoutMs, ChainWindow tolerated = ChainWindow.None)
        => new(window, ChainExpectation.Closes, timeoutMs, tolerated, NoPrompt);

    public static ChainStep Prompt(uint promptRow, int timeoutMs, ChainWindow tolerated = ChainWindow.None)
        => new(ChainWindow.SelectYesno, ChainExpectation.Opens, timeoutMs, tolerated, promptRow);
}

internal readonly record struct ChainView(ChainWindow Visible, uint PromptRow);

internal readonly record struct ChainVerdict(ChainAction Action, ChainWindow Window, NeedsPlayerReason Reason)
{
    public static readonly ChainVerdict Wait = new(ChainAction.Wait, ChainWindow.None, NeedsPlayerReason.None);

    public static ChainVerdict Of(ChainAction action, ChainWindow window) => new(action, window, NeedsPlayerReason.None);

    public static ChainVerdict Halt(ChainWindow window, NeedsPlayerReason reason) => new(ChainAction.Halt, window, reason);
}

internal sealed class RetainerChain
{
    public const uint GilLimitPromptRow = 13520;
    public const uint GilLimitPromptAlternateRow = 13521;

    private const ChainWindow PromptWindow = ChainWindow.SelectYesno;
    private const ChainWindow AlwaysAllowed = ChainWindow.InputNumeric;
    private const ChainWindow DutyPop = ChainWindow.ContentsFinderConfirm;

    private Phase phase;
    private ChainVerdict settled;

    private enum Phase : byte { Idle, Watching, Settled }

    public ChainStep Step { get; private set; }

    public long DeadlineMs { get; private set; }

    public bool Retried { get; private set; }

    public static bool IsGilLimitPrompt(uint promptRow) => promptRow is GilLimitPromptRow or GilLimitPromptAlternateRow;

    public void Begin(in ChainStep step, long nowMs)
    {
        Validate(step);
        Step = step;
        DeadlineMs = nowMs + step.TimeoutMs;
        Retried = false;
        phase = Phase.Watching;
    }

    public ChainVerdict Poll(in ChainView view, long nowMs)
    {
        if (phase == Phase.Idle)
        {
            throw new InvalidOperationException("The chain is polled before any step began.");
        }

        if (phase == Phase.Settled)
        {
            return settled;
        }

        if (TryFindIntrusion(view, out var intrusion))
        {
            return Settle(intrusion);
        }

        if (Arrived(view.Visible))
        {
            return Settle(ChainVerdict.Of(ChainAction.Next, Step.Window));
        }

        if (nowMs < DeadlineMs)
        {
            return ChainVerdict.Wait;
        }

        if (Retried)
        {
            return Settle(ChainVerdict.Of(ChainAction.Unwind, Step.Window));
        }

        Retried = true;
        DeadlineMs = nowMs + Step.TimeoutMs;
        return ChainVerdict.Of(ChainAction.Retry, Step.Window);
    }

    private bool TryFindIntrusion(in ChainView view, out ChainVerdict verdict)
    {
        if (ChainWindows.Contains(view.Visible, PromptWindow))
        {
            if (IsGilLimitPrompt(view.PromptRow))
            {
                verdict = ChainVerdict.Halt(PromptWindow, NeedsPlayerReason.GilLimitPrompt);
                return true;
            }

            if (Step.PromptRow == ChainStep.NoPrompt || view.PromptRow != Step.PromptRow)
            {
                verdict = ChainVerdict.Halt(PromptWindow, NeedsPlayerReason.UnexpectedWindow);
                return true;
            }
        }

        var stray = view.Visible & ~(Step.Window | Step.Tolerated | AlwaysAllowed | PromptWindow);
        if (stray == ChainWindow.None)
        {
            verdict = default;
            return false;
        }

        var foreign = stray & ~(ChainUnwind.Owned | DutyPop);
        if (foreign != ChainWindow.None)
        {
            verdict = ChainVerdict.Halt(First(foreign), NeedsPlayerReason.UnexpectedWindow);
            return true;
        }

        verdict = ChainWindows.Contains(stray, DutyPop)
            ? ChainVerdict.Of(ChainAction.Pause, DutyPop)
            : ChainVerdict.Of(ChainAction.Unwind, ChainUnwind.NextToClose(stray));
        return true;
    }

    private bool Arrived(ChainWindow visible)
    {
        var shown = ChainWindows.Contains(visible, Step.Window);
        return Step.Expectation == ChainExpectation.Opens ? shown : !shown;
    }

    private ChainVerdict Settle(in ChainVerdict verdict)
    {
        settled = verdict;
        phase = Phase.Settled;
        return verdict;
    }

    private static ChainWindow First(ChainWindow windows) => (ChainWindow)(1u << BitOperations.TrailingZeroCount((uint)windows));

    private static void Validate(in ChainStep step)
    {
        if (!BitOperations.IsPow2((uint)step.Window))
        {
            throw new ArgumentException("A step names exactly one window.", nameof(step));
        }

        if (step.Window == PromptWindow && step.PromptRow == ChainStep.NoPrompt)
        {
            throw new ArgumentException("A step that waits on a prompt names the prompt.", nameof(step));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step.TimeoutMs);
    }
}
