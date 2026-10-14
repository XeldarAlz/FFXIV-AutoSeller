using AutoSeller.Core.AutoRetainer.Model;

namespace AutoSeller.Core.Tasks.Model;

internal enum StartBlock : byte
{
    None,
    NotLoggedIn,
    NotOnHomeWorld,
    InContent,
    AutoRetainerBusy,
    UnresolvedJournalEntry,
    CompatibilityGatesClosed,
    NoRules,
    NoEnabledRetainers,
    Settling,
}

internal readonly record struct StartFacts(
    RunMode Mode,
    bool LoggedIn,
    bool OnHomeWorld,
    bool InContent,
    AutoRetainerStatus AutoRetainer,
    bool UnresolvedJournalEntry,
    bool CompatibilityGatesOpen,
    bool HasRules,
    bool HasEnabledRetainers,
    bool Settling);

internal static class StartBlockRule
{
    public static StartBlock Evaluate(in StartFacts facts)
    {
        if (!facts.LoggedIn)
        {
            return StartBlock.NotLoggedIn;
        }

        if (!facts.OnHomeWorld)
        {
            return StartBlock.NotOnHomeWorld;
        }

        if (facts.InContent)
        {
            return StartBlock.InContent;
        }

        if (facts.Mode == RunMode.Standalone && facts.AutoRetainer.Busy)
        {
            return StartBlock.AutoRetainerBusy;
        }

        if (facts.UnresolvedJournalEntry)
        {
            return StartBlock.UnresolvedJournalEntry;
        }

        if (!facts.CompatibilityGatesOpen)
        {
            return StartBlock.CompatibilityGatesClosed;
        }

        if (!facts.HasRules)
        {
            return StartBlock.NoRules;
        }

        if (!facts.HasEnabledRetainers)
        {
            return StartBlock.NoEnabledRetainers;
        }

        return facts.Settling ? StartBlock.Settling : StartBlock.None;
    }
}
