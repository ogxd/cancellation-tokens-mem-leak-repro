public interface ILinkedCancellationTokenProvider
{
    CancellationToken GetLinkedCancellationToken(CancellationToken token1, CancellationToken token2);
}