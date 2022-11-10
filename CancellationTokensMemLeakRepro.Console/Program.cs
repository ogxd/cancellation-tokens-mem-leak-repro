// See https://aka.ms/new-console-template for more information

using CancellationTokensMemLeakRepro;

Console.WriteLine("Start");

var tests = new Tests();
tests.TestDuration = TimeSpan.FromSeconds(20);

await tests.Test(new CoalescingTimeoutCancellationTokenProvider(), new FrameworkLinkedCancellationTokenProvider());

Console.WriteLine("Finished");