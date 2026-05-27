using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MinimalActorSystem.Benchmarks;

public sealed class SequentialPingPongTest : IBenchmarkTest
{
    public string Name => "Последовательный Ping-Pong";
    public string Description => "Пары ping/pong обмениваются сообщениями последовательно. "
        + "В любой момент активны только 2 актора. "
        + "Тест измеряет скорость последовательной цепочки сообщений.";

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

    public async Task RunAsync(Meter meter)
    {
        const int pairs = 3000;
        const int messagesPerPair = 5000;

        // Прикладные метрики
        var roundsCounter = meter.CreateCounter<long>("ping_pong.rounds", description: "Completed rounds");
        var pingFailedCounter = meter.CreateCounter<long>("ping_pong.ping_failed", description: "Failed ping deliveries");
        var pongFailedCounter = meter.CreateCounter<long>("ping_pong.pong_failed", description: "Failed pong deliveries");

        var monitor = new ResourceMonitor();
        Console.WriteLine($"Конфигурация: {pairs} пар, {messagesPerPair} сообщений на пару");
        Console.WriteLine($"Всего акторов: {pairs * 2}, всего сообщений: {(long)pairs * messagesPerPair * 2:N0}");

        var system = new ActorSystem(new Settings())
        {
            Metrics = new SystemMetrics(meter)
        };

        var allDone = new TaskCompletionSource<bool>();
        var counter = new Counter { Value = pairs };

        Console.WriteLine($"\nСоздаю {pairs * 2} акторов...");
        var swCreation = Stopwatch.StartNew();

        var startMessages = new StartMessage[pairs];
        var pingMessages = new PingMessage[pairs];
        var pongMessages = new PongMessage[pairs];
        var pingActors = new List<PingActor>(pairs);
        var pongActors = new List<PongActor>(pairs);

        for (int i = 0; i < pairs; i++)
        {
            var pongUid = Guid.NewGuid();
            var pingUid = Guid.NewGuid();

            startMessages[i] = new StartMessage(SystemUids.System, pingUid);
            pingMessages[i] = new PingMessage(pingUid, pongUid);
            pongMessages[i] = new PongMessage(pongUid, pingUid);

            var pong = new PongActor(system, pongUid, $"pong-{i}", pongMessages[i]);
            var ping = new PingActor(system, pingUid, $"ping-{i}", pongUid, counter, allDone, messagesPerPair);
            ping.Init(pingMessages[i], pongMessages[i]);

            system.RegisterActor(pong);
            system.RegisterActor(ping);

            pingActors.Add(ping);
            pongActors.Add(pong);
        }

        swCreation.Stop();
        monitor.Snapshot(out long memAfterCreate, out long peakAfterCreate, out int threadsAfterCreate, out int pendingAfterCreate, out long allocAfterCreate);
        monitor.PrintStats("После создания", memAfterCreate, peakAfterCreate, threadsAfterCreate, pendingAfterCreate, allocAfterCreate);
        Console.WriteLine($"  Время создания: {swCreation.ElapsedMilliseconds} мс");

        Console.WriteLine($"\nОтправляю стартовые сообщения {pairs} акторам...");
        var swWork = Stopwatch.StartNew();

        for (int i = 0; i < pairs; i++)
        {
            system.Send(startMessages[i]);
        }

        Console.WriteLine("Ожидание завершения...");
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(120));

        swWork.Stop();

        monitor.Snapshot(out long memAfterWork, out long peakAfterWork, out int threadsAfterWork, out int pendingAfterWork, out long allocAfterWork);
        monitor.PrintStats("После нагрузки", memAfterWork, peakAfterWork, threadsAfterWork, pendingAfterWork, allocAfterWork);

        long totalMessages = (long)pairs * messagesPerPair * 2;
        double totalUs = swWork.Elapsed.TotalMicroseconds;
        int totalPingFailed = pingActors.Sum(p => p.SendFailed);
        int totalPongFailed = pongActors.Sum(p => p.SendFailed);

        // Прикладные метрики
        roundsCounter.Add(pairs);
        pingFailedCounter.Add(totalPingFailed);
        pongFailedCounter.Add(totalPongFailed);

        Console.WriteLine();
        Console.WriteLine("=== Результаты производительности ===");
        Console.WriteLine($"  Сообщений всего:      {totalMessages:N0}");
        Console.WriteLine($"  Недоставлено ping:    {totalPingFailed}");
        Console.WriteLine($"  Недоставлено pong:    {totalPongFailed}");
        Console.WriteLine($"  Время работы:         {swWork.ElapsedMilliseconds:N0} мс");
        Console.WriteLine($"  На сообщение:         {totalUs / totalMessages:F2} мкс");
        Console.WriteLine($"  Сообщений в секунду:  {totalMessages / (swWork.ElapsedMilliseconds / 1000.0):N0}");

        Console.WriteLine("\nЗавершение системы...");
        var swShutdown = Stopwatch.StartNew();
        system.Shutdown();
        await system.WaitForShutdownAsync();
        swShutdown.Stop();

        monitor.Snapshot(out long memAfterShutdown, out long peakFinal, out int threadsAfterShutdown, out int pendingAfterShutdown, out long allocAfterShutdown);
        monitor.PrintStats("После завершения", memAfterShutdown, peakFinal, threadsAfterShutdown, pendingAfterShutdown, allocAfterShutdown);
        Console.WriteLine($"  Время завершения: {swShutdown.ElapsedMilliseconds} мс");
    }
}
