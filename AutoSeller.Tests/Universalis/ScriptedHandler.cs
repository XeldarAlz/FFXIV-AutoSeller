using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AutoSeller.Tests.Universalis;

internal sealed record SeenRequest(string Uri, string UserAgent);

internal sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] script) : HttpMessageHandler
{
    private readonly Lock gate = new();
    private readonly TaskCompletionSource hangStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int inFlight;

    public List<SeenRequest> Requests { get; } = [];

    public TimeSpan HoldFor { get; init; }

    public int HangingRequests { get; init; }

    public Task HangStarted => hangStarted.Task;

    public int MostInFlight { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int index;
        lock (gate)
        {
            index = Requests.Count;
            Requests.Add(new SeenRequest(request.RequestUri!.ToString(), request.Headers.UserAgent.ToString()));
            inFlight++;
            MostInFlight = Math.Max(MostInFlight, inFlight);
        }

        try
        {
            if (index < HangingRequests)
            {
                hangStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (HoldFor > TimeSpan.Zero)
            {
                await Task.Delay(HoldFor, cancellationToken);
            }

            return script[Math.Min(index, script.Length - 1)](request);
        }
        finally
        {
            lock (gate)
            {
                inFlight--;
            }
        }
    }
}
