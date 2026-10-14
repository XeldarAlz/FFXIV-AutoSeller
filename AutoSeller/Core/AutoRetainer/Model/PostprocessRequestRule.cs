using AutoSeller.Core.Tasks.Model;

namespace AutoSeller.Core.AutoRetainer.Model;

internal enum PostprocessSkip : byte
{
    None,
    Off,
    StoodDown,
    RunActive,
    UnknownRetainer,
    Blocked,
    NoWork,
    Unavailable,
}

internal readonly record struct PostprocessRequestFacts(
    bool Enabled,
    bool AutoRetainerAvailable,
    bool SellNowArmed,
    bool StoodDown,
    bool RunActive,
    bool RetainerKnown,
    StartBlock Block,
    int Work);

internal static class PostprocessRequestRule
{
    public static PostprocessSkip Evaluate(in PostprocessRequestFacts facts)
    {
        if (!facts.Enabled && !facts.SellNowArmed)
        {
            return PostprocessSkip.Off;
        }

        if (!facts.AutoRetainerAvailable)
        {
            return PostprocessSkip.Unavailable;
        }

        if (facts.StoodDown)
        {
            return PostprocessSkip.StoodDown;
        }

        if (facts.RunActive)
        {
            return PostprocessSkip.RunActive;
        }

        if (!facts.RetainerKnown)
        {
            return PostprocessSkip.UnknownRetainer;
        }

        if (facts.Block != StartBlock.None)
        {
            return PostprocessSkip.Blocked;
        }

        return facts.Work > 0 ? PostprocessSkip.None : PostprocessSkip.NoWork;
    }
}
