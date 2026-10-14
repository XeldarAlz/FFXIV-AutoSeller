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
    private readonly ReferenceFetches fetches = new();
    private CancellationTokenSource? cancellation;
    private ReferenceSnapshot? latest;
    private Task? fetch;
    private long lastManualRefreshMs = RefreshCooldown.NeverMs;
    private volatile bool disposed;

    public ReferenceService(string version)
    {
        client = new UniversalisClient(version);
        Svc.ClientState.Logout += OnLogout;
    }

    public ReferenceSnapshot? Latest => Volatile.Read(ref latest);

    public ReferenceSnapshot? Current => ReferenceSnapshot.ForHomeWorld(Latest, HomeWorldId);

    public TaxRatesResult Taxes => Current?.Taxes ?? TaxRatesResult.FailedWith(UniversalisFailure.NotFound);

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

    public TimeSpan CooldownRemaining => RefreshCooldown.Remaining(Volatile.Read(ref lastManualRefreshMs), Environment.TickCount64, Cooldown);

    private static uint HomeWorldId => Svc.PlayerState.HomeWorld.RowId;

    public ReferencePrice ReferenceFor(ItemKey item, CompetitorSnapshot? competition, DateTime nowUtc)
    {
        return ReferenceSnapshot.ReferenceFor(Latest, HomeWorldId, item, competition, nowUtc);
    }

    public ReferenceRefresh RequestRefresh(ReadOnlySpan<ItemKey> items)
    {
        if (CooldownRemaining > TimeSpan.Zero)
        {
            return ReferenceRefresh.CoolingDown;
        }

        var refresh = Fetch(items);
        if (refresh == ReferenceRefresh.Started)
        {
            Volatile.Write(ref lastManualRefreshMs, Environment.TickCount64);
        }

        return refresh;
    }

    public ReferenceRefresh Fetch(ReadOnlySpan<ItemKey> items)
    {
        if (items.IsEmpty)
        {
            return ReferenceRefresh.NothingToFetch;
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

        ReferenceFetchTicket ticket;
        lock (gate)
        {
            if (disposed)
            {
                return ReferenceRefresh.NothingToFetch;
            }

            CancelInFlight();
            ticket = fetches.Begin(scope, itemIds);
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var previous = latest;
            fetch = Task.Run(() => FetchAsync(ticket, previous, token), token);
        }

        RunLog.Info($"References: fetching Universalis prices for {ticket.ItemCount} item(s) on {scope.Segment} in {ticket.Chunks.Length} request(s), with its tax rates; {ticket.CarriedCount} item(s) carried over from the fetch it replaced.");
        return ReferenceRefresh.Started;
    }

    public void Dispose()
    {
        Svc.ClientState.Logout -= OnLogout;
        lock (gate)
        {
            disposed = true;
            CancelInFlight();
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

    private void OnLogout(int type, int code)
    {
        lock (gate)
        {
            CancelInFlight();
            fetches.Reset();
            Volatile.Write(ref latest, null);
        }

        RunLog.Info("References: the Universalis prices were dropped on logout.");
    }

    private void CancelInFlight()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
    }

    private async Task FetchAsync(ReferenceFetchTicket ticket, ReferenceSnapshot? previous, CancellationToken token)
    {
        try
        {
            var references = client.FetchAllReferencesAsync(ticket.Chunks, token);
            var taxes = client.FetchTaxRatesAsync(ticket.Scope.Segment, token);
            await Task.WhenAll(references, taxes).ConfigureAwait(false);
            var results = await references.ConfigureAwait(false);
            var rates = await taxes.ConfigureAwait(false);
            var built = ReferenceSnapshot.Build(ticket.Generation, DateTime.UtcNow, ticket.Scope, results, rates, previous);
            lock (gate)
            {
                if (disposed || !fetches.TryFinish(ticket.Generation))
                {
                    return;
                }

                Volatile.Write(ref latest, built);
            }

            RunLog.Info($"References ready: generation {built.Generation}, {built.Table.Items.Length} item(s) on {ticket.Scope.Segment}, {built.FailedRequestCount} of {built.RequestCount} request(s) failed, {DescribeTaxes(rates)}.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            RunLog.Debug($"References: fetch {ticket.Generation} was replaced or stopped.");
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                fetches.TryFinish(ticket.Generation);
            }

            if (!disposed)
            {
                RunLog.Error(exception, "References: the Universalis fetch failed");
            }
        }
    }
}
