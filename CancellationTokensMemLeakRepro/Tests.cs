using MathNet.Numerics;
using NUnit.Framework;

namespace CancellationTokensMemLeakRepro;

public class Tests
{
    public static IEnumerable<ITimeoutCancellationTokenProvider> TimeoutTokenProviders
    {
        get
        {
            yield return new FrameworkTimeoutCancellationTokenProvider();
            yield return new CoalescingTimeoutCancellationTokenProvider();
            //yield return new PooledTimeoutCancellationTokenProvider();
        }
    }
    
    public static IEnumerable<ILinkedCancellationTokenProvider> LinkedTokenProviders
    {
        get
        {
            yield return new FrameworkLinkedCancellationTokenProvider();
            yield return new CustomLinkedCancellationTokenProvider();
            yield return new CustomNoRegistrationLinkedCancellationTokenProvider();
        }
    }

    public TimeSpan TestDuration { get; set; } = TimeSpan.FromSeconds(20);

    [Test]
    [NonParallelizable]
    public async Task Test(
        [ValueSource(nameof(TimeoutTokenProviders))] ITimeoutCancellationTokenProvider timeoutCancellationTokenProvider,
        [ValueSource(nameof(LinkedTokenProviders))] ILinkedCancellationTokenProvider linkedCancellationTokenProvider)
    {
        var testCts = new CancellationTokenSource();
        testCts.CancelAfter(TestDuration);

        var memoryUsageMeasurement = new List<double>();
            
        // Just a timer to measure memory usage every second
        var timer = new Timer(_ =>
        {
            // Avoid biais when measuring memory as much as possible (not ideal but does the job)
            GC.Collect();
            long bytes = GC.GetTotalMemory(true);
            memoryUsageMeasurement.Add(bytes);
        }, null, TimeSpan.FromSeconds(5) /* let it warmup a bit */, TimeSpan.FromSeconds(1));

        await Task.WhenAll(Enumerable.Range(0, 1000)
            .Select(i => RunManyAsync(timeoutCancellationTokenProvider, linkedCancellationTokenProvider, testCts.Token)));

        // Calculate memory increase per second using slope from line fitting
        (_, double slope) = Fit.Line(Enumerable.Range(0, memoryUsageMeasurement.Count).Select(i => (double)i).ToArray(), memoryUsageMeasurement.ToArray());
        
        Console.WriteLine($"Memory increase per second: {slope} bytes");
        
        // Above some threshold, we can assume there is a memory leak
        Assert.Less(slope, 1_000_000, $"Probably leaking");
        
        GC.KeepAlive(timer);
    }

    private async Task RunManyAsync(
        ITimeoutCancellationTokenProvider timeoutTp,
        ILinkedCancellationTokenProvider linkedTp,
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var token2 = timeoutTp.GetCancellationToken(TimeSpan.FromMilliseconds(Random.Shared.Next(1, 5000)));
                var subTasktoken = linkedTp.GetLinkedCancellationToken(token, token2);
                
                await RunAsync(timeoutTp, linkedTp, subTasktoken);
            }
            catch (OperationCanceledException)
            {
                
            }
        }
    }

    private async Task RunAsync(
        ITimeoutCancellationTokenProvider timeoutTp,
        ILinkedCancellationTokenProvider linkedTp,
        CancellationToken token)
    {
        // Probablity to fork task and token
        if (Random.Shared.NextDouble() > 0.05)
        {
            var token2 = timeoutTp.GetCancellationToken(TimeSpan.FromMilliseconds(Random.Shared.Next(1, 5000)));
            token = linkedTp.GetLinkedCancellationToken(token, token2);

            await RunAsync(timeoutTp, linkedTp, token);
        }
        
        await Task.Delay(Random.Shared.Next(1, 1000), token);
    }
}