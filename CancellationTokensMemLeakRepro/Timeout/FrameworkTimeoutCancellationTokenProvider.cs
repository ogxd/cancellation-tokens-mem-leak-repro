public class FrameworkTimeoutCancellationTokenProvider : ITimeoutCancellationTokenProvider
{
    public static readonly FrameworkTimeoutCancellationTokenProvider Instance;

    static FrameworkTimeoutCancellationTokenProvider()
    {
        Instance = new FrameworkTimeoutCancellationTokenProvider();
    }
    
    public CancellationToken GetCancellationToken(TimeSpan cancelAfter) => new CancellationTokenSource(cancelAfter).Token;
}