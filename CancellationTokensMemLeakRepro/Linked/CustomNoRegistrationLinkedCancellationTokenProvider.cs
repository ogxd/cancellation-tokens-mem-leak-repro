public class CustomNoRegistrationLinkedCancellationTokenProvider : ILinkedCancellationTokenProvider
{
    public CancellationToken GetLinkedCancellationToken(CancellationToken token1, CancellationToken token2)
    {
        return new LinkedCancellationTokenSourceNoRegistration(token1, token2).Token;
    }
}

public class LinkedCancellationTokenSourceNoRegistration : CancellationTokenSource
{ 
    public LinkedCancellationTokenSourceNoRegistration(CancellationToken token1, CancellationToken token2)
    {
        if (token1.CanBeCanceled)
            token1.Register(Cancel);
                
        if (token2.CanBeCanceled)
            token2.Register(Cancel);
    }
}