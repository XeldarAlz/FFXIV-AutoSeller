using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AutoSeller.Core.Market.Model;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Core.Pricing.Universalis;
using AutoSeller.Core.Shared;
using ECommons.DalamudServices;

namespace AutoSeller.Core.Pricing;

internal enum ReferenceRefresh : byte
{
    Started,
    CoolingDown,
    NothingToFetch,
    NoHomeWorld,
}

internal sealed class ReferenceService : IDisposable
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    private readonly UniversalisClient client;
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private ReferenceSnapshot? latest;
    private Task? fetch;
    private long generation;
    private long lastStartMs = RefreshCooldown.NeverMs;
    private volatile bool disposed;

    public ReferenceService(string version)
    {
        client = new UniversalisClient(version);
    }

    public ReferenceSnapshot? Latest => Volatile.Read(ref latest);

    public bool Fetching
    {
        get
        {
            lock (gate)
            {
                return fetch is { IsCompleted: false };
            }
        }
    }

    public TimeSpan CooldownRemaining => RefreshCooldown.Remaining(Volatile.Read(ref lastStartMs), Environment.TickCount64, Cooldown);

    public ReferencePrice ReferenceFor(ItemKey item, CompetitorSnapshot? competition, DateTime nowUtc)
    {
        return ReferenceSnapshot.ReferenceFor(Latest, Svc.PlayerState.HomeWorld.RowId, item, competition, nowUtc);
    }

    public ReferenceRefresh TryRefresh(ReadOnlySpan<ItemKey> items)
    {
        if (CooldownRemaining > TimeSpan.Zero)
        {
            return ReferenceRefresh.CoolingDown;
        }

        if (!TryHomeWorld(out var scope))
        {
            return ReferenceRefresh.NoHomeWorld;
        }

        var itemIds = new uint[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            itemIds[index] = items[index].ItemId;
        }

        var chunks = ReferenceChunk.Split(scope, itemIds);
        if (chunks.IsEmpty)
        {
            return ReferenceRefresh.NothingToFetch;
        }

        lock (gate)
        {
            if (disposed)
            {
                return ReferenceRefresh.NothingToFetch;
            }

            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var fetchGeneration = ++generation;
            var previous = latest;
            Volatile.Write(ref lastStartMs, Environment.TickCount64);
            fetch = Task.Run(() => FetchAsync(fetchGeneration, scope, chunks, previous, token), token);
        }

        RunLog.Info($"References: fetching Universalis prices for {itemIds.Length} item key(s) on {scope.Segment} in {chunks.Length} request(s), with its tax rates.");
        return ReferenceRefresh.Started;
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
        }

        client.Dispose();
    }

    private static bool TryHomeWorld(out UniversalisScope scope)
    {
        scope = default;
        var home = Svc.PlayerState.HomeWorld;
        if (!Svc.PlayerState.IsLoaded || home.RowId == 0 || !home.TryGetValue(out var world))
        {
            return false;
        }

        var name = world.Name.ExtractText();
        if (name.Length == 0)
        {
            return false;
        }

        scope = UniversalisScope.ForWorld(home.RowId, name);
        return true;
    }

    private static string DescribeTaxes(in TaxRatesResult taxes)
    {
        if (taxes.Failed)
        {
            return $"tax rates not read ({taxes.Failure})";
        }

        var rates = taxes.Rates;
        return $"tax rates Limsa Lominsa {rates.LimsaLominsa}%, Gridania {rates.Gridania}%, Ul'dah {rates.UlDah}%, Ishgard {rates.Ishgard}%, Kugane {rates.Kugane}%, Crystarium {rates.Crystarium}%, Old Sharlayan {rates.OldSharlayan}%, Tuliyollal {rates.Tuliyollal}%";
    }

    private async Task FetchAsync(long fetchGeneration, UniversalisScope scope, ImmutableArray<ReferenceChunk> chunks, ReferenceSnapshot? previous, CancellationToken token)
    {
        try
        {
            var references = client.FetchAllReferencesAsync(chunks, token);
            var taxes = client.FetchTaxRatesAsync(scope.Segment, token);
            await Task.WhenAll(references, taxes).ConfigureAwait(false);
            var results = await references.ConfigureAwait(false);
            var rates = await taxes.ConfigureAwait(false);
            var built = ReferenceSnapshot.Build(fetchGeneration, DateTime.UtcNow, scope, results, rates, previous);
            lock (gate)
            {
                if (fetchGeneration != generation || disposed)
                {
                    return;
                }

                Volatile.Write(ref latest, built);
            }

            RunLog.Info($"References ready: generation {built.Generation}, {built.Table.Items.Length} item(s) on {scope.Segment}, {built.FailedRequestCount} of {built.RequestCount} request(s) failed, {DescribeTaxes(rates)}.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            RunLog.Debug($"References: fetch {fetchGeneration} was replaced or stopped.");
        }
        catch (Exception exception)
        {
            if (!disposed)
            {
                RunLog.Error(exception, "References: the Universalis fetch failed");
            }
        }
    }
}
