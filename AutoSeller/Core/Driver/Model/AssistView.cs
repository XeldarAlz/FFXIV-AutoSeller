using AutoSeller.Core.Pricing.Model;

namespace AutoSeller.Core.Driver.Model;

internal enum AssistStatus : byte
{
    Idle,
    Comparing,
    Decided,
    ItemUnknown,
    SearchFailed,
}

internal sealed record AssistView(long Revision, AssistStatus Status, string ItemName, AssistSuggestion Suggestion, Decision Decision, TimeSpan CooldownLeft)
{
    public static AssistView Idle { get; } = new(0, AssistStatus.Idle, string.Empty, default, default, TimeSpan.Zero);
}
