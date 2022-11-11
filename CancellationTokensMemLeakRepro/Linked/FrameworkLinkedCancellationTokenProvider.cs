public class FrameworkLinkedCancellationTokenProvider : ILinkedCancellationTokenProvider
{
    public CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken token1, CancellationToken token2)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(token1, token2);
    }
}