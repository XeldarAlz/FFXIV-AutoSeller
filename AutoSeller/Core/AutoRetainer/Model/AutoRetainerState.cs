namespace AutoSeller.Core.AutoRetainer.Model;

internal enum AutoRetainerState : byte { Missing, Outdated, Busy, Available }

internal enum BusyAnswer : byte { NoAnswer, Idle, Busy }

internal readonly record struct AutoRetainerProbe(bool Installed, bool GatesAnswered, BusyAnswer Busy);

internal static class AutoRetainerStateRule
{
    public static AutoRetainerState Classify(in AutoRetainerProbe probe)
    {
        if (probe.Busy == BusyAnswer.Busy)
        {
            return AutoRetainerState.Busy;
        }

        if (probe.GatesAnswered && probe.Busy == BusyAnswer.Idle)
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
