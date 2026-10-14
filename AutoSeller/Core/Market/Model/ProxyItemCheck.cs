namespace AutoSeller.Core.Market.Model;

internal readonly record struct ProxyRequestIds(byte Next, byte Current);

internal enum ProxyItemVerdict : byte
{
    NotTaken = 0,
    SameItem = 1,
    OtherItem = 2,
}

internal static class ProxyItemCheck
{
    public static ProxyItemVerdict Check(ProxyRequestIds atPress, ProxyRequestIds now, uint rawProxyItemId, uint expectedItemId)
    {
        if (now == atPress)
        {
            return ProxyItemVerdict.NotTaken;
        }

        return MarketItemId.Normalize(rawProxyItemId) == expectedItemId ? ProxyItemVerdict.SameItem : ProxyItemVerdict.OtherItem;
    }
}
