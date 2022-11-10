using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// A cancellation token source that coalesces many timeouts into one, approximate
/// Returned cancellation token is actually shared between all callees that happened to fall in one group.
/// It allows decoupling number of timers created by CancallationTokenSource.CancelAfter from the traffic
/// <remarks>
/// Implementation inspired by https://devblogs.microsoft.com/pfxteam/coalescing-cancellationtokens-from-timeouts/
/// </remarks>
/// </summary>
public class CoalescingTimeoutCancellationTokenProvider : ITimeoutCancellationTokenProvider
{
    private long _toleranceMilliseconds;
    public long ToleranceMilliseconds => _toleranceMilliseconds;

    private readonly ConcurrentDictionary<long, CancellationToken> _timeToToken = new ConcurrentDictionary<long, CancellationToken>();

    public static CoalescingTimeoutCancellationTokenProvider Instance;

    static CoalescingTimeoutCancellationTokenProvider()
    {
        Instance = new CoalescingTimeoutCancellationTokenProvider();
    }

    public CoalescingTimeoutCancellationTokenProvider(int toleranceMilliseconds = 20)
    {
        _toleranceMilliseconds = toleranceMilliseconds;

        if (!Stopwatch.IsHighResolution)
            throw new InvalidOperationException(
                $"{nameof(CoalescingTimeoutCancellationTokenProvider)} will not work properly when Stopwatch is not high resolution");
    }

    public CancellationToken GetCancellationToken(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            return new CancellationToken(true);

        var timestampMs = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / (double)1_000);
        var timeoutMs = timeout.TotalMilliseconds;
        var timestampIndex = (long)Math.Floor((timestampMs + timeoutMs) / ToleranceMilliseconds);

        var ceiledDeltaMs = ToleranceMilliseconds - (timestampMs + timeoutMs) % ToleranceMilliseconds;
        var ceiledTimeout = TimeSpan.FromMilliseconds(timeoutMs + ceiledDeltaMs);

        return _timeToToken.GetOrAdd(timestampIndex, x =>
        {
            var cts = new CancellationTokenSource(ceiledTimeout);
            var token = cts.Token;
            token.Register(__ => {
                _timeToToken.TryRemove(x, out _);
                cts.Dispose();
            }, x);
            return token;
        });
    }
}