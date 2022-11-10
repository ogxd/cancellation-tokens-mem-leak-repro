public interface ITimeoutCancellationTokenProvider
{
    CancellationToken GetCancellationToken(TimeSpan cancelAfter);
}