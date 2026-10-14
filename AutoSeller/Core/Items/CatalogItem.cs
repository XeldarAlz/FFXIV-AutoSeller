namespace AutoSeller.Core.Items;

[Flags]
internal enum CatalogItemFlags : byte
{
    None = 0,
    CanBeHighQuality = 1 << 0,
}

internal readonly record struct CatalogItem(uint Id, string Name, uint IconId, CatalogItemFlags Flags)
{
    public bool CanBeHighQuality => (Flags & CatalogItemFlags.CanBeHighQuality) != 0;
}

internal readonly record struct CatalogCategory(byte Id, string Name, int IconId);
