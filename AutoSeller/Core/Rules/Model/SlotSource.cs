namespace AutoSeller.Core.Rules.Model;

internal enum SlotSource : byte
{
    Bags,
    Crystals,
    RetainerPage,
    Saddlebag,
    Armoury,
}

[Flags]
internal enum SellSources : byte
{
    None = 0,
    Bags = 1 << (int)SlotSource.Bags,
    Crystals = 1 << (int)SlotSource.Crystals,
    RetainerPages = 1 << (int)SlotSource.RetainerPage,
    Saddlebag = 1 << (int)SlotSource.Saddlebag,
    Armoury = 1 << (int)SlotSource.Armoury,
    Default = Bags | Crystals,
}
