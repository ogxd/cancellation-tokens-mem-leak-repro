using System.Diagnostics;

/// <summary>
/// A very simple yet very performant solution for pooling cancellation tokens.
/// Thread safe with very little contention involved.
/// </summary>
public class PooledTimeoutCancellationTokenProvider : ITimeoutCancellationTokenProvider
{
    private static readonly double TicksPerMillisecond = Stopwatch.Frequency / (double)1_000;

    private readonly int _maximumDurationMs;
    private readonly int _toleranceMs;
    private readonly PooledTokenSource[] _tokenSources;

    public static readonly PooledTimeoutCancellationTokenProvider Instance;

    // Guaranteed to execute only once per domain, thus granting thread safety
    static PooledTimeoutCancellationTokenProvider()
    {
        Instance = new PooledTimeoutCancellationTokenProvider();
    }
        
    /// <summary>
    /// Creates a instance of a PooledCancellationTokenProvider that pools tokens in order to reduce number of active timers (and thread contention that goes with it)
    /// The theoritical maximum number of active timers created by this class is maximumDurationMilliseconds / toleranceMilliseconds.
    /// </summary>
    /// <param name="maximumDurationMilliseconds">The maximum timeout duration this class will be able to pool, in milliseconds.
    /// For instance, with value of 10000 (10s), a token for a timeout of 9s will be pooled, but a token for a timeout of 11s won't.
    /// Make sure this is high enough for most of your timeout needs.
    /// The lower the duration is, the less will be the maximum number of active timers (less active timers = less thread contention)
    /// If 99% of your timeouts are under a second, then you can use a maximum duration of 1000 (1s) for instance.
    /// </param>
    /// <param name="toleranceMilliseconds">The tolerance for requested timeouts, in milliseconds
    /// For instance, with a tolerance of 100ms, a timeout of 15ms and a timeout of 69ms will both return the same token that expires in 100ms.
    /// If tolerance is 50ms, then first for the first timeout a token expiring in 50ms will be returned, and a token expiring in 100ms will be returned for the second timeout.
    /// Make sure this is appropriate with your needs. For instance, if you don't need a precision under 100ms, then use 100ms.
    /// It won't hardly have any effect under 15ms because of precision of underlying task timers (dotnet thing).
    /// The higher the tolerance is, the less will be the maximum number of active timers (less active timers = less thread contention)
    /// </param>
    public PooledTimeoutCancellationTokenProvider(int maximumDurationMilliseconds = 5_000, int toleranceMilliseconds = 20)
    {
        _toleranceMs = toleranceMilliseconds;
        _maximumDurationMs = Ceil(maximumDurationMilliseconds, toleranceMilliseconds);
        _tokenSources = new PooledTokenSource[_maximumDurationMs / toleranceMilliseconds];

        // Initializes array with expired token sources, so that we don't have to deal with nulls later
        for (int i = 0; i < _tokenSources.Length; i++)
        {
            _tokenSources[i] = new PooledTokenSource();
        }
    }

    /// <summary>
    /// Returns a cancellation token that will expire once the given duration has elapsed.
    /// This token will be pooled unless its duration above the maximum duration supported by this class.
    /// Note that the given duration is approximated up to the tolerance configured for this class (see the toleranceMilliseconds parameter of the constructor for more information).
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public CancellationToken GetCancellationToken(TimeSpan timeout)
    {
        /* HOW IT WORKS:
         * Let's say maximumDurationMilliseconds is 5000 (5s) and tolerance 1000 (1s). The internal array would be:
         * [ token expiring in 1s, token expiring in 2s, token expiring 3s, token expiring in 4s, token expiring in 5s ], or for simplicity:
         * [ 1s, 2s, 3s, 4s, 5s ]
         * Let's say we request a token expiring in 2.5s. With the modulo and indexing logic, we will return token expiring in 3s
         * [ 1s, 2s, 3s, 4s, 5s ]
         *           ^
         * Now let's say 2s has elapsed, array would look like:
         * [ 0s, 0s, 1s, 2s, 3s ]
         * And now if we want again another token expiring in 2.5s, you can see that it can't be the same index as before, because it now points to a token expiring in 1s
         * What we do is use a stopwatch to count the elapsed time into account in order to compute the index. Since 2s have elapsed, it basically means that index in incremented by 2:
         * [ 0s, 0s, 1s, 2s, 3s ]
         *           ------> ^
         * Finally, what it does if we were to request a token that is currently expired (0s) is create a new token source with the appropriate duration.
         */

        // Above the supported maximum duration, tokens will no longer be pooled
        if (timeout.TotalMilliseconds >= _maximumDurationMs - _toleranceMs)
            return new CancellationTokenSource(timeout).Token;

        if (timeout <= TimeSpan.Zero)
            return new CancellationToken(canceled: true);

        long timestampTicks = Stopwatch.GetTimestamp();

        var timestampMs = timestampTicks / (Stopwatch.Frequency / (double)1_000);
        var timeoutMs = timeout.TotalMilliseconds;
        var index = (long)Math.Floor((timestampMs + timeoutMs) / _toleranceMs) % _tokenSources.Length;

        var tokenSource = _tokenSources[index];
            
        if (tokenSource.IsTimeoutElapsed(timestampTicks))
        {
            lock (tokenSource)
            {
                if (tokenSource.IsTimeoutElapsed(timestampTicks))
                {
                    var ceiledDeltaMs = _toleranceMs - (timestampMs + timeoutMs) % _toleranceMs;
                    tokenSource.Restart(timestampTicks, (int)Math.Ceiling(timeoutMs + ceiledDeltaMs));
                }
            }
        }

        return tokenSource.CancellationToken;
    }

    internal static int Ceil(double value, int precision) => (int)(precision * Math.Ceiling(value / precision));

    private class PooledTokenSource
    {
        private long _timestamp;
        private volatile int _timeoutMs;
        private volatile CancellationTokenSource _cts;

        internal PooledTokenSource()
        {
            _cts = new CancellationTokenSource();
            _cts.Cancel();
        }

        internal void Restart(long timestamp, int timeoutMs)
        {
            // Cancels in case cancellation take is late
            _cts.Cancel();

            _timeoutMs = timeoutMs;
            _timestamp = timestamp;
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            _cts.CancelAfter(timeoutMs);
        }

        internal CancellationToken CancellationToken => _cts.Token;

        internal bool IsTimeoutElapsed(long timestamp) => (timestamp - _timestamp) >= _timeoutMs * TicksPerMillisecond;
    }
}