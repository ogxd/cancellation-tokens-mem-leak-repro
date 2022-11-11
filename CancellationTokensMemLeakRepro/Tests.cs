using MathNet.Numerics;
using NUnit.Framework;
using System.Diagnostics;

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
            //yield return new CustomNoRegistrationLinkedCancellationTokenProvider();
        }
    }

    public TimeSpan TestDuration { get; set; } = TimeSpan.FromSeconds(20);

    [Test]
    [NonParallelizable]
    public async Task Test(
        [ValueSource(nameof(TimeoutTokenProviders))] ITimeoutCancellationTokenProvider timeoutCancellationTokenProvider,
        [ValueSource(nameof(LinkedTokenProviders))] ILinkedCancellationTokenProvider linkedCancellationTokenProvider,
        [Values] bool diposeSource)
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

        var iterations = await Task.WhenAll(Enumerable.Range(0, 1000)
            .Select(i => RunAsync(timeoutCancellationTokenProvider, linkedCancellationTokenProvider, diposeSource, testCts.Token)));

        // Calculate memory increase per second using slope from line fitting
        (_, double slope) = Fit.Line(Enumerable.Range(0, memoryUsageMeasurement.Count).Select(i => (double)i).ToArray(), memoryUsageMeasurement.ToArray());
        
        Console.WriteLine($"Memory increase per second: {slope} bytes");
        Console.WriteLine($"Iterations: {iterations.Sum()}");
        
        // Above some threshold, we can assume there is a memory leak
        Assert.Less(slope, 100_000, $"Probably leaking");
        
        GC.KeepAlive(timer);
    }

    private async Task<int> RunAsync(
        ITimeoutCancellationTokenProvider timeoutTp,
        ILinkedCancellationTokenProvider linkedTp,
        bool disposeSource,
        CancellationToken token)
    {
        var random = new Random(0);
        int iterations = 0;

        while (!token.IsCancellationRequested)
        {
            var token2 = timeoutTp.GetCancellationToken(TimeSpan.FromMilliseconds(random.Next(1, 500)));

            var subTasktokenCts = linkedTp.GetLinkedCancellationTokenSource(token, token2);
            var subTasktoken = subTasktokenCts.Token;

            Interlocked.Increment(ref iterations);

            try
            {
                await Task.Delay(TimeSpan.FromDays(1), subTasktoken);
            }
            catch (OperationCanceledException)
            {
                
            }
            finally
            {
                if (disposeSource)
                    subTasktokenCts.Dispose();
            }
        }

        return iterations;
    }

    [Test]
    [Timeout(5000)]
    public async Task LinkedTokenProviderWorksProperly([ValueSource(nameof(LinkedTokenProviders))] ILinkedCancellationTokenProvider linkedCancellationTokenProvider)
    {
        var cts = linkedCancellationTokenProvider.GetLinkedCancellationTokenSource(new CancellationTokenSource(1000).Token, new CancellationTokenSource(3000).Token);
        var token = cts.Token;

        var sw = Stopwatch.StartNew();
        token.Register(() => sw.Stop());

        while (!token.IsCancellationRequested) {
            await Task.Delay(100);
        }

        Assert.That(sw.ElapsedMilliseconds, Is.EqualTo(1000).Within(100));
    }
}