public interface ILinkedCancellationTokenProvider
{
    CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken token1, CancellationToken token2);
}