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
        if (Enum.IsDefined(AssistSkipKey))
        {
            return false;
        }

        AssistSkipKey = AssistSkipKey.Shift;
        return true;
    }
}
