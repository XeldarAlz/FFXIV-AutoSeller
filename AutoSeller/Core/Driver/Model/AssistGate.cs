namespace AutoSeller.Core.Driver.Model;

internal enum AssistBlock : byte
{
    None,
    AssistOff,
    RunOwnsWindows,
    OtherTaskRunning,
    SkipKeyHeld,
    ReadingOff,
}

internal readonly record struct AssistFacts(bool AssistOn, bool RunOwnsWindows, bool OtherTaskRunning, bool SkipKeyHeld, bool ReadingAllowed);

internal static class AssistGate
{
    public static AssistBlock Check(in AssistFacts facts)
    {
        if (!facts.AssistOn)
        {
            return AssistBlock.AssistOff;
        }

        if (facts.RunOwnsWindows)
        {
            return AssistBlock.RunOwnsWindows;
        }

        if (facts.OtherTaskRunning)
        {
            return AssistBlock.OtherTaskRunning;
        }

        if (facts.SkipKeyHeld)
        {
            return AssistBlock.SkipKeyHeld;
        }

        return facts.ReadingAllowed ? AssistBlock.None : AssistBlock.ReadingOff;
    }
}
