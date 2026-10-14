using AutoSeller.Core.Driver.Model;
using AutoSeller.Core.Tasks.Model;
using System.Threading.Tasks;

namespace AutoSeller.Core.Driver;

internal sealed partial class RetainerDriver
{
    private const int ClickWaitMs = 3_000;
    private const int PromptCloseMs = 3_000;
    private const int StepPollFrames = 2;
    private const ChainWindow RetainerBackdrop = ChainWindow.RetainerList | ChainWindow.SelectString;
    private const ChainWindow SellListBackdrop = RetainerBackdrop | ChainWindow.RetainerSellList;

    private static readonly Func<bool> noClick = static () => true;

    private enum StepEnd : byte
    {
        Done,
        Missed,
        Halted,
        Cancelled,
    }

    private async Task<StepResult> Step(ChainStep step, Func<bool> act, Func<bool>? whileWaiting = null)
    {
        await DelayMs(PacingPlan.UiStepMs(Random.Shared.NextDouble()));
        if (CancelToken.IsCancellationRequested)
        {
            return StepResult.Cancelled;
        }

        await ClickOnce(act);
        return await Watch(step, act, whileWaiting);
    }

    private Task<StepResult> Watch(ChainStep step, Func<bool>? whileWaiting = null)
    {
        return Watch(step, noClick, whileWaiting);
    }

    private async Task<StepResult> Watch(ChainStep step, Func<bool> retry, Func<bool>? whileWaiting)
    {
        chain.Begin(step, Environment.TickCount64);
        while (!CancelToken.IsCancellationRequested)
        {
            if (UnexpectedAddons.InCombat())
            {
                return StepResult.Halt(new Stop(DriverHalt.CombatOrDutyPop, $"combat while waiting for {step.Window} to {Expected(step)}"));
            }

            var verdict = chain.Poll(UnexpectedAddons.View(), Environment.TickCount64);
            switch (verdict.Action)
            {
                case ChainAction.Wait:
                    whileWaiting?.Invoke();
                    await NextFrame(StepPollFrames);
                    continue;
                case ChainAction.Retry:
                    Diag($"Driver: {step.Window} did not {Expected(step)} within {step.TimeoutMs} ms; trying once more");
                    await ClickOnce(retry);
                    continue;
                case ChainAction.Next:
                    return StepResult.Done;
                case ChainAction.Unwind:
                    Warn($"Driver: {step.Window} did not {Expected(step)}{(verdict.Window == step.Window ? " after a second try" : $"; {verdict.Window} of this plugin is in the way")}");
                    return StepResult.Missed(verdict.Window);
                case ChainAction.Pause:
                    return StepResult.Halt(new Stop(DriverHalt.CombatOrDutyPop, $"{verdict.Window} opened while waiting for {step.Window}"));
                default:
                    return StepResult.Halt(await HaltFor(verdict, step));
            }
        }

        return StepResult.Cancelled;
    }

    private async Task<Stop> HaltFor(ChainVerdict verdict, ChainStep step)
    {
        if (verdict.Reason != NeedsPlayerReason.GilLimitPrompt)
        {
            return new Stop(DriverHalt.UnexpectedWindow, $"{verdict.Window} opened while waiting for {step.Window}");
        }

        Warn("Driver: the retainer gil-limit prompt is open; answering No");
        if (!await RetainerWindows.RepeatUntil(RetainerClicks.DeclinePrompt, static () => !RetainerClicks.IsReady(RetainerClicks.PromptAddon), PromptCloseMs, unloaded))
        {
            Warn("Driver: the gil-limit prompt did not close after No");
        }

        return new Stop(DriverHalt.GilLimitPrompt, "the retainer gil-limit prompt");
    }

    private async Task<bool> ClickOnce(Func<bool> click)
    {
        var deadline = Environment.TickCount64 + ClickWaitMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (click())
            {
                return true;
            }

            await NextFrame();
        }

        return false;
    }

    private static string Expected(ChainStep step) => step.Expectation == ChainExpectation.Opens ? "open" : "close";

    private readonly record struct StepResult(StepEnd End, ChainWindow Window, Stop Stop)
    {
        public static StepResult Done => new(StepEnd.Done, ChainWindow.None, Stop.None);

        public static StepResult Cancelled => new(StepEnd.Cancelled, ChainWindow.None, Stop.None);

        public bool Succeeded => End == StepEnd.Done;

        public bool Halted => End == StepEnd.Halted;

        public static StepResult Missed(ChainWindow window) => new(StepEnd.Missed, window, Stop.None);

        public static StepResult Halt(in Stop stop) => new(StepEnd.Halted, ChainWindow.None, stop);
    }
}
