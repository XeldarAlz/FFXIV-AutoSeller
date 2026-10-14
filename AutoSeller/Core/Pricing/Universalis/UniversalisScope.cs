namespace AutoSeller.Core.Pricing.Universalis;

internal enum UniversalisScopeKind : byte
{
    World,
    DataCenter,
}

internal readonly record struct UniversalisScope(UniversalisScopeKind Kind, string Segment, uint WorldId)
{
    public bool CoversWorld => Kind == UniversalisScopeKind.World;

    public static UniversalisScope ForWorld(uint worldId, string worldName)
    {
        return new UniversalisScope(UniversalisScopeKind.World, worldName, worldId);
    }

    public static UniversalisScope ForDataCenter(string dataCenterName)
    {
        return new UniversalisScope(UniversalisScopeKind.DataCenter, dataCenterName, 0);
    }
}
