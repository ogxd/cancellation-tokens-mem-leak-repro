public class CustomLinkedCancellationTokenProvider : ILinkedCancellationTokenProvider
{
    public CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken token1, CancellationToken token2)
    {
        return new LinkedCancellationTokenSource(token1, token2);
    }
}

public class LinkedCancellationTokenSource : CancellationTokenSource
{
    private readonly CancellationTokenRegistration? _registration1;
    private readonly CancellationTokenRegistration? _registration2;
            
    public LinkedCancellationTokenSource(CancellationToken token1, CancellationToken token2)
    {
        if (token1.CanBeCanceled)
            _registration1 = token1.Register(CancelAndUnregister);
                
        if (token2.CanBeCanceled)
            _registration2 = token2.Register(CancelAndUnregister);
    }

    private void CancelAndUnregister()
    {
        Cancel();
        _registration1?.Unregister();
        _registration2?.Unregister();
    }
}