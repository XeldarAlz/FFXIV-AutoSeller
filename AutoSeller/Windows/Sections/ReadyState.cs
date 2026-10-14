using AutoSeller.Core.Localization;
using AutoSeller.Core.Tasks;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AutoSeller.Windows.Sections;

internal static class ReadyState
{
    public enum Kind { SetupNeeded, Ready, Blocked, Running, Paused }

    public readonly record struct Info(Kind Kind, Vector4 Accent, Vector4 AccentSoft);

    private static int cachedFrame = -1;
    private static Info cached;

    public static Info Resolve(SellController controller)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == cachedFrame)
        {
            return cached;
        }

        cached = Compute(controller);
        cachedFrame = frame;
        return cached;
    }

    private static Info Compute(SellController controller)
    {
        if (controller.Running)
        {
            return controller.Paused
                ? new Info(Kind.Paused, Styling.AccentAmber, Styling.AccentAmberSoft)
                : new Info(Kind.Running, Styling.AccentBlue, Styling.AccentBlueSoft);
        }

        return RunReadiness.Evaluate() switch
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
        RunPhase.Preparing => Loc.T(L.Run.PhasePreparing),
        RunPhase.Paused    => Loc.T(L.Run.PhasePaused),
        _                  => Loc.T(L.Run.PhaseStandingBy),
    };
}
