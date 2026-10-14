using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using AutoSeller.Core.Tasks.Model;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal static class ReadyState
{
    public enum Kind { SetupNeeded, Ready, Blocked, Running, Paused }

    public readonly record struct Info(Kind Kind, Vector4 Accent, Vector4 AccentSoft);

    private static int cachedFrame = -1;
    private static Info cached;

    public static Info Resolve(Plugin plugin)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == cachedFrame)
        {
            return cached;
        }

        cached = Compute(plugin);
        cachedFrame = frame;
        return cached;
    }

    private static Info Compute(Plugin plugin)
    {
        var controller = plugin.Controller;
        if (controller.Running)
        {
            return controller.Paused
                ? new Info(Kind.Paused, Styling.AccentAmber, Styling.AccentAmberSoft)
                : new Info(Kind.Running, Styling.AccentBlue, Styling.AccentBlueSoft);
        }

        return RunReadiness.Evaluate(plugin.Ledger.BlocksWrites, plugin.AutoRetainer.Status.Busy) switch
        {
            Readiness.Ready => new Info(Kind.Ready, Styling.AccentMint, Styling.AccentMintSoft),
            Readiness.SetupNeeded => new Info(Kind.SetupNeeded, Styling.AccentRose, Styling.AccentRoseSoft),
            _ => new Info(Kind.Blocked, Styling.AccentNebula, Styling.AccentGlowSoft),
        };
    }

    public static string ShortLabel(Kind kind) => kind switch
    {
        Kind.Running     => Loc.T(L.Shell.StatusRunning),
        Kind.Paused      => Loc.T(L.Shell.StatusPaused),
        Kind.Ready       => Loc.T(L.Shell.StatusReady),
        Kind.Blocked     => Loc.T(L.Shell.StatusBlocked),
        Kind.SetupNeeded => Loc.T(L.Shell.StatusSetupNeeded),
        _                => Loc.T(L.Shell.StatusIdle),
    };

    public static string PhaseLabel(RunPhase phase) => phase switch
    {
        RunPhase.Preparing  => Loc.T(L.Run.PhasePreparing),
        RunPhase.Paused     => Loc.T(L.Run.PhasePaused),
        RunPhase.Travelling => Loc.T(L.Run.PhaseTravelling),
        RunPhase.Selling    => Loc.T(L.Run.PhaseSelling),
        RunPhase.Finishing  => Loc.T(L.Run.PhaseFinishing),
        RunPhase.Stopping   => Loc.T(L.Run.PhaseStopping),
        _                  => Loc.T(L.Run.PhaseStandingBy),
    };
}
