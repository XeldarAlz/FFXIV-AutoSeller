using AutoSeller.Core.Shared;

namespace AutoSeller;

public enum AssistSkipKey : byte
{
    Shift,
    Control,
    Alt,
}

public sealed partial class Configuration
{
    public bool AssistEnabled { get; set; } = false;

    public AssistSkipKey AssistSkipKey { get; set; } = AssistSkipKey.Shift;

    private bool RepairAssistSettings()
    {
        var changed = SettingRepair.KnownValue(AssistSkipKey, AssistSkipKey.Shift, out var skipKey);
        AssistSkipKey = skipKey;
        return changed;
    }
}
