public class CustomLinkedCancellationTokenProvider : ILinkedCancellationTokenProvider
{
    public CancellationToken GetLinkedCancellationToken(CancellationToken token1, CancellationToken token2)
    {
        return new LinkedCancellationTokenSource(token1, token2).Token;
    }
}

public class LinkedCancellationTokenSource : CancellationTokenSource
{
    private readonly CancellationTokenRegistration? reg1;
    private readonly CancellationTokenRegistration? reg2;
            
    public LinkedCancellationTokenSource(CancellationToken token1, CancellationToken token2)
    {
        if (token1.CanBeCanceled)
            reg1 = token1.Register(Cancel);
                
        if (token2.CanBeCanceled)
            reg2 = token2.Register(Cancel);
    }

    protected override void Dispose(bool disposing)
    {
        reg1?.Dispose();
        reg2?.Dispose();
                
        base.Dispose(disposing);
    }

    ~LinkedCancellationTokenSource()
    {
        Dispose(true);
    }
}