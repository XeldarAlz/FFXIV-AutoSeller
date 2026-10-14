using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Shared;

namespace AutoSeller.Core.Pricing.Universalis;

internal sealed class ReferenceSnapshot
{
    public required long Generation { get; init; }

    public required DateTime FetchedAtUtc { get; init; }

    public required UniversalisReferenceTable Table { get; init; }

    public required TaxRatesResult Taxes { get; init; }

    public required int RequestCount { get; init; }

    public required int FailedRequestCount { get; init; }

    public UniversalisScope Scope => Table.Scope;

    public static ReferenceSnapshot? ForHomeWorld(ReferenceSnapshot? snapshot, uint homeWorldId)
    {
        return snapshot is not null && snapshot.IsFor(homeWorldId) ? snapshot : null;
    }

    public static ReferenceSnapshot Build(
        long generation,
        DateTime fetchedAtUtc,
        UniversalisScope scope,
        IReadOnlyList<ReferenceChunkResult> results,
        in TaxRatesResult taxes,
        ReferenceSnapshot? previous)
    {
        var sameScope = previous is not null && previous.Scope == scope;
        return new ReferenceSnapshot
        {
            Generation = generation,
            FetchedAtUtc = fetchedAtUtc,
            Table = UniversalisReferenceTable.Merge(previous?.Table, scope, results),
            Taxes = taxes.Failed && sameScope && !previous!.Taxes.Failed ? previous.Taxes : taxes,
            RequestCount = results.Count,
            FailedRequestCount = CountFailed(results),
        };
    }

    public static ReferencePrice ReferenceFor(ReferenceSnapshot? snapshot, uint homeWorldId, ItemKey item, CompetitorSnapshot? competition, DateTime nowUtc)
    {
        if (ForHomeWorld(snapshot, homeWorldId) is not { } current || !current.Table.TryGet(item.ItemId, out var reference))
        {
            return ReferenceSelector.Select(item, default, default, competition, nowUtc);
        }

        return ReferenceSelector.Select(item, reference.World(item.IsHq), reference.DataCenter(item.IsHq), competition, nowUtc);
    }

    public bool IsFor(uint homeWorldId)
    {
        return homeWorldId != 0 && Scope.CoversWorld && Scope.WorldId == homeWorldId;
    }

    public TimeSpan Age(DateTime nowUtc)
    {
        var age = nowUtc - FetchedAtUtc;
        return age > TimeSpan.Zero ? age : TimeSpan.Zero;
    }

    private static int CountFailed(IReadOnlyList<ReferenceChunkResult> results)
    {
        var failed = 0;
        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].Failed)
            {
                failed++;
            }
        }

        return failed;
    }
}
