namespace AutoSeller.Core.Rules.Model;

[Flags]
public enum QualityFilter : byte
{
    Normal = 1,
    High = 2,
    Both = Normal | High,
}

[Serializable]
public sealed class ItemRule
{
    public const byte DefaultListingsToKeepUp = 1;
    public const byte MaximumListingsToKeepUp = 20;

    public uint ItemId { get; set; }

    public QualityFilter Qualities { get; set; } = QualityFilter.Both;

    public uint Floor { get; set; }

    public uint Ceiling { get; set; }

    public uint FixedPrice { get; set; }

    public uint LotSize { get; set; }

    public uint KeepCount { get; set; }

    public byte ListingsToKeepUp { get; set; } = DefaultListingsToKeepUp;

    public ulong PreferredRetainerId { get; set; }

    public bool Paused { get; set; }
}
