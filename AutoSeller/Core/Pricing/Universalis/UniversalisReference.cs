using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Universalis;

[Flags]
internal enum ReferenceCoverage : byte
{
    None = 0,
    RequestFailed = 1 << 0,
    ItemFailed = 1 << 1,
    NotReturned = 1 << 2,
    InvalidValues = 1 << 3,
}

internal readonly record struct UniversalisReference(
    uint ItemId,
    ReferencePrice WorldNormal,
    ReferencePrice WorldHigh,
    ReferencePrice DataCenterNormal,
    ReferencePrice DataCenterHigh,
    ReferenceCoverage Coverage)
{
    public const ReferenceCoverage FailureCoverage = ReferenceCoverage.RequestFailed | ReferenceCoverage.ItemFailed | ReferenceCoverage.NotReturned;

    public bool Failed => (Coverage & FailureCoverage) != ReferenceCoverage.None;

    public ReferencePrice World(bool isHq)
    {
        return isHq ? WorldHigh : WorldNormal;
    }

    public ReferencePrice DataCenter(bool isHq)
    {
        return isHq ? DataCenterHigh : DataCenterNormal;
    }

    public static UniversalisReference WithoutData(uint itemId, ReferenceCoverage coverage)
    {
        var normal = NoPrice(new ItemKey(itemId, false));
        var high = NoPrice(new ItemKey(itemId, true));
        return new UniversalisReference(itemId, normal, high, normal, high, coverage);
    }

    public static ReferencePrice NoPrice(ItemKey item)
    {
        return new ReferencePrice(item, ReferenceSource.None, 0, 0, 0, 0, default);
    }
}
