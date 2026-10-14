namespace AutoSeller.Core.AutoRetainer.Model;

internal enum AutoRetainerState : byte { Missing, Outdated, Available }

internal enum BusyAnswer : byte { NoAnswer, Idle, Busy }

internal readonly record struct AutoRetainerProbe(bool Installed, bool GatesAnswered, BusyAnswer Busy);

internal readonly record struct AutoRetainerStatus(AutoRetainerState Readiness, bool Busy);

internal static class AutoRetainerStateRule
{
    public static AutoRetainerStatus Classify(in AutoRetainerProbe probe) => new(Readiness(probe), probe.Busy == BusyAnswer.Busy);

    private static AutoRetainerState Readiness(in AutoRetainerProbe probe)
    {
        if (probe.GatesAnswered && probe.Busy != BusyAnswer.NoAnswer)
        {
            return AutoRetainerState.Available;
        }

        if (probe.Installed || probe.GatesAnswered || probe.Busy != BusyAnswer.NoAnswer)
        {
            return AutoRetainerState.Outdated;
        }

        return AutoRetainerState.Missing;
    }
}
