namespace AutoSeller.Core.Execution.Model;

internal enum VendorGilSource : byte
{
    Character,
    Retainer,
}

internal static class VendorGil
{
    public const VendorGilSource Source = VendorGilSource.Character;
}
