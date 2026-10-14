namespace AutoSeller.Core.Retainers.Model;

internal readonly record struct RetainerInfo(
    ulong RetainerId,
    string Name,
    byte Town,
    uint Gil,
    byte MarketItemCount,
    bool Available);
