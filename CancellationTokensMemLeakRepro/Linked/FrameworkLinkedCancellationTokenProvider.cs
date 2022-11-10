public class FrameworkLinkedCancellationTokenProvider : ILinkedCancellationTokenProvider
{
    public CancellationToken GetLinkedCancellationToken(CancellationToken token1, CancellationToken token2)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(token1, token2).Token;
    }
}