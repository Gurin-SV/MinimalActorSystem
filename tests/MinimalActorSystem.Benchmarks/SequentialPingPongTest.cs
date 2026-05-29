using System.Diagnostics;

namespace MinimalActorSystem.Benchmarks;

public sealed class SequentialPingPongTest : IBenchmarkTest
{
    public string Name => Localization.SequentialPingPongName;
    public string Description => Localization.SequentialPingPongDesc;

    private class Counter { public int Value; }

    private sealed class StartMessage(Guid sender, Guid receiver) : Letter(sender, receiver);
    private sealed class PingMessage(Guid sender, Guid receiver) : Letter(sender, receiver);
    private sealed class PongMessage(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class PingActor(IActorSystem system, Guid uid, string name, Guid pongUid, Counter counter,
        TaskCompletionSource<bool> done, int messagesPerPair)
        : Actor(system, uid, name)
    {
        private int _received;
        private PingMessage? _pingMessage;
        private PongMessage? _pongMessage;
        private int _sendFailed;

        public int SendFailed => _sendFailed;

        public void Init(PingMessage pingMessage, PongMessage pongMessage)
        {
            _pingMessage = pingMessage;
            _pongMessage = pongMessage;
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case StartMessage:
                    SendPing();
                    break;
                case PongMessage:
                    _received++;
                    if (_received >= messagesPerPair)
                    {
                        int remaining = Interlocked.Decrement(ref counter.Value);
                        if (remaining == 0)
                            done.TrySetResult(true);
                    }
                    else
                    {
                        SendPing();
                    }
                    break;
            }
            return default;
        }

        private void SendPing()
        {
            _pingMessage!.Receiver = pongUid;
            if (!System.Send(_pingMessage))
                Interlocked.Increment(ref _sendFailed);
        }
    }

    private sealed class PongActor(IActorSystem system, Guid uid, string name, PongMessage pongMessage)
        : Actor(system, uid, name)
    {
        private readonly PongMessage _pongMessage = pongMessage;
        private int _sendFailed;

        public int SendFailed => _sendFailed;

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is PingMessage ping)
            {
                _pongMessage.Receiver = ping.Sender;
                if (!System.Send(_pongMessage))
                    Interlocked.Increment(ref _sendFailed);
            }
            return default;
        }
    }

    public async Task RunAsync()
    {
        const int pairs = 3000;
        const int messagesPerPair = 5000;

        ResourceMonitor monitor = new();
        Console.WriteLine(Localization.ConfigFormat(pairs, messagesPerPair));
        Console.WriteLine(Localization.TotalActorsFormat(pairs * 2, (long)pairs * messagesPerPair * 2));

        ActorSystem system = new(new Settings());
        TaskCompletionSource<bool> allDone = new();
        Counter counter = new() { Value = pairs };

        Console.WriteLine($"\n{Localization.CreatingActors}");
        Stopwatch swCreation = Stopwatch.StartNew();

        StartMessage[] startMessages = new StartMessage[pairs];
        PingMessage[] pingMessages = new PingMessage[pairs];
        PongMessage[] pongMessages = new PongMessage[pairs];
        List<PingActor> pingActors = new(pairs);
        List<PongActor> pongActors = new(pairs);

        for (int i = 0; i < pairs; i++)
        {
            Guid pongUid = Guid.NewGuid();
            Guid pingUid = Guid.NewGuid();

            startMessages[i] = new StartMessage(SystemUids.System, pingUid);
            pingMessages[i] = new PingMessage(pingUid, pongUid);
            pongMessages[i] = new PongMessage(pongUid, pingUid);

            PongActor pong = new(system, pongUid, $"pong-{i}", pongMessages[i]);
            PingActor ping = new(system, pingUid, $"ping-{i}", pongUid, counter, allDone, messagesPerPair);
            ping.Init(pingMessages[i], pongMessages[i]);

            system.RegisterActor(pong);
            system.RegisterActor(ping);

            pingActors.Add(ping);
            pongActors.Add(pong);
        }

        swCreation.Stop();
        monitor.Snapshot(out long memAfterCreate, out long peakAfterCreate, out int threadsAfterCreate, out int pendingAfterCreate, out long allocAfterCreate);
        monitor.PrintStats(Localization.AfterCreation, memAfterCreate, peakAfterCreate, threadsAfterCreate, pendingAfterCreate, allocAfterCreate);
        Console.WriteLine($"  {Localization.CreationTime}: {swCreation.ElapsedMilliseconds} {Localization.MilliSeconds}");

        Console.WriteLine($"\n{Localization.SendingStartMessages} {pairs} {Localization.ActorsLower}...");
        Stopwatch swWork = Stopwatch.StartNew();

        for (int i = 0; i < pairs; i++)
        {
            system.Send(startMessages[i]);
        }

        Console.WriteLine(Localization.WaitingCompletion);
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(120));

        swWork.Stop();

        monitor.Snapshot(out long memAfterWork, out long peakAfterWork, out int threadsAfterWork, out int pendingAfterWork, out long allocAfterWork);
        monitor.PrintStats(Localization.AfterLoad, memAfterWork, peakAfterWork, threadsAfterWork, pendingAfterWork, allocAfterWork);

        long totalMessages = (long)pairs * messagesPerPair * 2;
        double totalUs = swWork.Elapsed.TotalMicroseconds;
        int totalPingFailed = pingActors.Sum(p => p.SendFailed);
        int totalPongFailed = pongActors.Sum(p => p.SendFailed);

        Console.WriteLine();
        Console.WriteLine("=== " + Localization.Results + " ===");
        Console.WriteLine($"  {Localization.MessagesTotal}: {totalMessages:N0}");
        Console.WriteLine($"  {Localization.FailedPing}: {totalPingFailed}");
        Console.WriteLine($"  {Localization.FailedPong}: {totalPongFailed}");
        Console.WriteLine($"  {Localization.WorkTime}: {swWork.ElapsedMilliseconds:N0} {Localization.MilliSeconds}");
        Console.WriteLine($"  {Localization.TimePerMessage}: {totalUs / totalMessages:F2} {Localization.MicroSeconds}");
        Console.WriteLine($"  {Localization.MessagesPerSecond}: {totalMessages / (swWork.ElapsedMilliseconds / 1000.0):N0}");

        Console.WriteLine($"\n{Localization.ShuttingDown}");
        Stopwatch swShutdown = Stopwatch.StartNew();
        system.Shutdown();
        await system.WaitForShutdownAsync();
        swShutdown.Stop();

        monitor.Snapshot(out long memAfterShutdown, out long peakFinal, out int threadsAfterShutdown, out int pendingAfterShutdown, out long allocAfterShutdown);
        monitor.PrintStats(Localization.AfterShutdown, memAfterShutdown, peakFinal, threadsAfterShutdown, pendingAfterShutdown, allocAfterShutdown);
        Console.WriteLine($"  {Localization.ShutdownTime}: {swShutdown.ElapsedMilliseconds} {Localization.MilliSeconds}");
    }
}
