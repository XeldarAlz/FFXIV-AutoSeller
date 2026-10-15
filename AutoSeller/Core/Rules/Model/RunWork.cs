namespace AutoSeller.Core.Rules.Model;

[Flags]
internal enum RunWork : byte
{
    None = 0,
    Reprice = 1,
    List = 2,
    Vendor = 4,
    Sell = List | Vendor,
    All = Reprice | List | Vendor,
}
