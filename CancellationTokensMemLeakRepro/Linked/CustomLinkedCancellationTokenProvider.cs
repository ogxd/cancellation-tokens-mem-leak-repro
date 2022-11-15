using System.Diagnostics;

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
         
    // first bit = canceled
    // second bit = disposed
    private int _state = 0;

    public LinkedCancellationTokenSource(CancellationToken token1, CancellationToken token2)
    {
        if (token1.CanBeCanceled)
            _registration1 = token1.Register(CancelAndUnregister);

        if (token2.CanBeCanceled)
            _registration2 = token2.Register(CancelAndUnregister);
    }

    private void CancelAndUnregister()
    {
        // Mark as canceled, but only cancel if not canceled or disposed before
        var state = Interlocked.Or(ref _state, 1);
        if (state == 0)
        {
            Debug.Assert(state < 2, "State should be default, and cancel nor not disposed nor both");
            Cancel();

            Unregister();
        }
    }

    protected override void Dispose(bool disposing)
    {
        // Mark as disposed, but dispose only once
        var state = Interlocked.Or(ref _state, 2);
        if ((state & 2) != 2)
        {
            Debug.Assert(state < 2, "State should either be default or canceled, but not disposed");

            Unregister();

            base.Dispose(disposing);
        }
    }

    private void Unregister()
    {
        _registration1?.Unregister();
        _registration2?.Unregister();
    }
}
