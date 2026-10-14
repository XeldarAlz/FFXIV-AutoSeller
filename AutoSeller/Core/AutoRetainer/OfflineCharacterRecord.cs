namespace AutoSeller.Core.AutoRetainer;

internal sealed class OfflineCharacterRecord
{
    public OfflineRetainerRecord?[]? RetainerData { get; set; }
}

internal sealed class OfflineRetainerRecord
{
    public ulong RetainerId { get; set; }
}
