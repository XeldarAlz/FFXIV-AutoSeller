using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using AutoSeller.Core.Pricing.Universalis;
using static AutoSeller.Tests.Universalis.UniversalisFixtures;

namespace AutoSeller.Tests.Universalis;

public sealed class UniversalisClientTests
{
    private const string Version = "0.1.0.0";
    private const string PhoenixFixture = "aggregated-world-phoenix.json";
    private const string TaxFixture = "tax-rates-phoenix.json";

    [Fact]
    public async Task RequestsCarryTheDescriptiveUserAgentAndTheAggregatedPath()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://universalis.app/api/v2/aggregated/Phoenix/5057,36112", request.Uri);
        Assert.Equal("AutoSeller/0.1.0.0 (+https://github.com/XeldarAlz/FFXIV-AutoSeller)", request.UserAgent);
        Assert.False(result.Failed);
        Assert.Equal(1_999u, Item(result, BusyItem).WorldNormal.LowestListing);
    }

    [Fact]
    public async Task RateLimitIsHonouredBeforeTheRetry()
    {
        var clock = new ManualClock();
        var handler = new ScriptedHandler(
            _ => Respond(HttpStatusCode.TooManyRequests, retryAfterSeconds: 2),
            _ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, clock);

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.False(result.Failed);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(TimeSpan.FromSeconds(2), clock.Delays);
        Assert.Equal(464u, Item(result, QuietItem).WorldNormal.LowestListing);
    }

    [Fact]
    public async Task ARetryAfterDateIsHonoured()
    {
        var clock = new ManualClock();
        var handler = new ScriptedHandler(
            _ => RespondRetryAt(HttpStatusCode.TooManyRequests, clock.GetUtcNow().AddSeconds(5)),
            _ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, clock);

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.False(result.Failed);
        Assert.Contains(TimeSpan.FromSeconds(5), clock.Delays);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NotFoundFailsTheRequestWithoutRetry(int itemCount)
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.NotFound));
        using var client = NewClient(handler, new ManualClock());
        var chunk = itemCount == 1 ? Chunk(PhoenixScope, BusyItem) : PhoenixChunk();

        var result = await client.FetchReferencesAsync(chunk, CancellationToken.None);

        Assert.Equal(UniversalisFailure.NotFound, result.Failure);
        Assert.Single(handler.Requests);
        Assert.Equal(itemCount, result.Items.Length);
        Assert.All(result.Items, item => Assert.Equal(ReferenceCoverage.RequestFailed, item.Coverage));
    }

    [Fact]
    public async Task BadRequestFailsTheRequestWithoutRetry()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.BadRequest));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(Chunk(PhoenixScope, GilItem), CancellationToken.None);

        Assert.Equal(UniversalisFailure.HttpError, result.Failure);
        Assert.Single(handler.Requests);
        Assert.True(Item(result, GilItem).Failed);
    }

    [Fact]
    public async Task AMalformedBodyFailsTheRequestWithoutRetry()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.OK, Utf8("<html>")));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.Equal(UniversalisFailure.Malformed, result.Failure);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ALongServerPauseFailsTheOtherRequestsAtOnceInsteadOfHoldingTheFetch()
    {
        var clock = new ManualClock();
        var handler = new ScriptedHandler(
            _ => Respond(HttpStatusCode.TooManyRequests, retryAfterSeconds: 3600),
            _ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, clock);

        var first = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);
        var second = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.Equal(UniversalisFailure.RateLimited, first.Failure);
        Assert.Equal(UniversalisFailure.ServerPaused, second.Failure);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain(clock.Delays, wait => wait > RequestThrottle.MaximumPause);
    }

    [Fact]
    public async Task AFetchUnderALongServerPausePublishesWhatItHasPromptly()
    {
        var clock = new ManualClock();
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.TooManyRequests, retryAfterSeconds: 3600));
        using var client = NewClient(handler, clock);
        await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);
        var chunks = ImmutableArray.Create(PhoenixChunk(), PhoenixChunk(), PhoenixChunk());

        var results = await client.FetchAllReferencesAsync(chunks, CancellationToken.None);

        Assert.Equal(3, results.Length);
        Assert.All(results, result => Assert.Equal(UniversalisFailure.ServerPaused, result.Failure));
        Assert.Single(handler.Requests);
        Assert.Empty(clock.Delays);
    }

    [Fact]
    public async Task ARateLimitOnTaxRatesPausesTheReferenceFetchesToo()
    {
        var handler = new ScriptedHandler(
            _ => Respond(HttpStatusCode.TooManyRequests, retryAfterSeconds: 3600),
            _ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, new ManualClock());

        var rates = await client.FetchTaxRatesAsync("Phoenix", CancellationToken.None);
        var references = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.Equal(UniversalisFailure.RateLimited, rates.Failure);
        Assert.Equal(UniversalisFailure.ServerPaused, references.Failure);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ServiceUnavailableWithALongRetryAfterIsLabelledAnHttpError()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.ServiceUnavailable, retryAfterSeconds: 3600));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.Equal(UniversalisFailure.HttpError, result.Failure);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AShortServerPauseIsStillHeld()
    {
        var clock = new ManualClock();
        var handler = new ScriptedHandler(
            _ => Respond(HttpStatusCode.TooManyRequests, retryAfterSeconds: 20),
            _ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, clock);

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.False(result.Failed);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains(TimeSpan.FromSeconds(20), clock.Delays);
    }

    [Fact]
    public async Task ServerErrorsAreRetriedABoundedNumberOfTimes()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.ServiceUnavailable));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.Equal(UniversalisFailure.HttpError, result.Failure);
        Assert.Equal(RetryPolicy.MaximumAttempts, handler.Requests.Count);
        Assert.True(Item(result, BusyItem).Failed);
    }

    [Fact]
    public async Task ARetryAfterLongerThanThePatienceEndsTheRequest()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.TooManyRequests, retryAfterSeconds: 600));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.Equal(UniversalisFailure.RateLimited, result.Failure);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ATransportErrorIsRetried()
    {
        var handler = new ScriptedHandler(
            _ => throw new HttpRequestException("reset"),
            _ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.False(result.Failed);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ATimedOutAttemptIsRetried()
    {
        var handler = new ScriptedHandler(
            _ => throw new TaskCanceledException("timed out"),
            _ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.False(result.Failed);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task EveryAttemptTimingOutIsReportedAsATimeout()
    {
        var handler = new ScriptedHandler(_ => throw new TaskCanceledException("timed out"));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchReferencesAsync(PhoenixChunk(), CancellationToken.None);

        Assert.Equal(UniversalisFailure.Timeout, result.Failure);
        Assert.Equal(RetryPolicy.MaximumAttempts, handler.Requests.Count);
    }

    [Fact]
    public async Task CancellationStopsTheFetch()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.OK, Read(PhoenixFixture)));
        using var client = NewClient(handler, new ManualClock());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.FetchReferencesAsync(PhoenixChunk(), cancellation.Token));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task NoMoreThanFourRequestsAreInFlight()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.OK, Read(PhoenixFixture))) { HoldFor = TimeSpan.FromMilliseconds(30) };
        using var client = NewClient(handler, new ManualClock());
        var chunks = ImmutableArray.CreateBuilder<ReferenceChunk>();
        for (var index = 0; index < 12; index++)
        {
            chunks.Add(PhoenixChunk());
        }

        var results = await client.FetchAllReferencesAsync(chunks.ToImmutable(), CancellationToken.None);

        Assert.Equal(12, results.Length);
        Assert.All(results, result => Assert.False(result.Failed));
        Assert.InRange(handler.MostInFlight, 1, RequestThrottle.DefaultConcurrency);
    }

    [Fact]
    public async Task TaxRatesAreFetchedForTheNamedWorld()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.OK, Read(TaxFixture)));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchTaxRatesAsync("Phoenix", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://universalis.app/api/v2/tax-rates?world=Phoenix", request.Uri);
        Assert.False(result.Failed);
        Assert.Equal(3, result.Rates.Tuliyollal);
        Assert.Equal(0, result.Rates.OldSharlayan);
    }

    [Fact]
    public async Task TaxRatesForAnUnknownWorldAreNotFound()
    {
        var handler = new ScriptedHandler(_ => Respond(HttpStatusCode.NotFound));
        using var client = NewClient(handler, new ManualClock());

        var result = await client.FetchTaxRatesAsync("Nowhere", CancellationToken.None);

        Assert.Equal(UniversalisFailure.NotFound, result.Failure);
        Assert.Single(handler.Requests);
    }

    private static ReferenceChunk PhoenixChunk()
    {
        return Chunk(PhoenixScope, BusyItem, QuietItem);
    }

    private static UniversalisClient NewClient(ScriptedHandler handler, ManualClock clock)
    {
        return new UniversalisClient(handler, Version, clock, clock.Delay, static () => 0.5);
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, byte[]? body = null, int retryAfterSeconds = -1)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body ?? []) };
        if (retryAfterSeconds >= 0)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds));
        }

        return response;
    }

    private static HttpResponseMessage RespondRetryAt(HttpStatusCode status, DateTimeOffset retryAt)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent([]) };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAt);
        return response;
    }
}
