using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AutoSeller.Core.Pricing.Universalis;

internal sealed class UniversalisClient : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(5);

    private const long MaximumResponseBytes = 4L * 1024 * 1024;
    private const string JsonMediaType = "application/json";

    private readonly HttpClient http;
    private readonly RequestThrottle throttle;
    private readonly TimeProvider time;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Func<double> jitter;

    public UniversalisClient(string version)
        : this(CreateHandler(), version, TimeProvider.System, null, null)
    {
    }

    public UniversalisClient(
        HttpMessageHandler handler,
        string version,
        TimeProvider time,
        Func<TimeSpan, CancellationToken, Task>? delay,
        Func<double>? jitter)
    {
        this.time = time;
        this.delay = delay ?? ((span, token) => Task.Delay(span, time, token));
        this.jitter = jitter ?? Random.Shared.NextDouble;
        http = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(UniversalisApi.BaseAddress),
            Timeout = Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = MaximumResponseBytes,
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UniversalisApi.UserAgent(version));
        http.DefaultRequestHeaders.Accept.ParseAdd(JsonMediaType);
        throttle = new RequestThrottle(RequestThrottle.DefaultConcurrency, RequestThrottle.DefaultInterval, time, this.delay);
    }

    public void Dispose()
    {
        http.Dispose();
        throttle.Dispose();
    }

    public async Task<ReferenceChunkResult[]> FetchAllReferencesAsync(ImmutableArray<ReferenceChunk> chunks, CancellationToken cancellationToken)
    {
        var tasks = new Task<ReferenceChunkResult>[chunks.Length];
        for (var index = 0; index < tasks.Length; index++)
        {
            tasks[index] = FetchReferencesAsync(chunks[index], cancellationToken);
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task<ReferenceChunkResult> FetchReferencesAsync(ReferenceChunk chunk, CancellationToken cancellationToken)
    {
        var reply = await GetAsync(UniversalisApi.AggregatedPath(chunk), cancellationToken).ConfigureAwait(false);
        return reply.Body is { } body
            ? UniversalisTranslator.TranslateReferences(body, chunk)
            : ReferenceChunkResult.FailedWith(chunk, reply.Failure);
    }

    public async Task<TaxRatesResult> FetchTaxRatesAsync(string world, CancellationToken cancellationToken)
    {
        var reply = await GetAsync(UniversalisApi.TaxRatesPath(world), cancellationToken).ConfigureAwait(false);
        return reply.Body is { } body
            ? UniversalisTranslator.TranslateTaxRates(body)
            : TaxRatesResult.FailedWith(reply.Failure);
    }

    private static SocketsHttpHandler CreateHandler()
    {
        return new SocketsHttpHandler
        {
            MaxConnectionsPerServer = RequestThrottle.DefaultConcurrency,
            PooledConnectionLifetime = ConnectionLifetime,
            AutomaticDecompression = DecompressionMethods.All,
        };
    }

    private async Task<Reply> GetAsync(string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var reply = await AttemptAsync(path, cancellationToken).ConfigureAwait(false);
            if (!reply.Retryable || attempt == RetryPolicy.MaximumAttempts)
            {
                return reply;
            }

            if (!RetryPolicy.TryGetDelay(reply.RetryAfter, attempt, jitter(), out var wait))
            {
                throttle.PauseFor(wait);
                return reply;
            }

            if (reply.Failure == UniversalisFailure.RateLimited)
            {
                throttle.PauseFor(wait);
            }

            await delay(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Reply> AttemptAsync(string path, CancellationToken cancellationToken)
    {
        using var lease = await throttle.EnterAsync(cancellationToken).ConfigureAwait(false);
        if (!lease.Granted)
        {
            return Reply.Final(UniversalisFailure.ServerPaused);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var response = await http.GetAsync(path, timeout.Token).ConfigureAwait(false);
            switch (RetryPolicy.Classify((int)response.StatusCode))
            {
                case ResponseClass.Success:
                    var body = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
                    return new Reply(body, UniversalisFailure.None, false, null);
                case ResponseClass.NotFound:
                    return Reply.Final(UniversalisFailure.NotFound);
                case ResponseClass.Retry:
                    var failure = response.StatusCode == HttpStatusCode.TooManyRequests ? UniversalisFailure.RateLimited : UniversalisFailure.HttpError;
                    return new Reply(null, failure, true, RetryAfterOf(response));
                default:
                    return Reply.Final(UniversalisFailure.HttpError);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Reply.Retry(UniversalisFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            return Reply.Retry(UniversalisFailure.HttpError);
        }
    }

    private TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            return date - time.GetUtcNow();
        }

        return null;
    }

    private readonly record struct Reply(byte[]? Body, UniversalisFailure Failure, bool Retryable, TimeSpan? RetryAfter)
    {
        public static Reply Final(UniversalisFailure failure)
        {
            return new Reply(null, failure, false, null);
        }

        public static Reply Retry(UniversalisFailure failure)
        {
            return new Reply(null, failure, true, null);
        }
    }
}
