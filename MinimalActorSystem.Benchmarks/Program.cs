using System.Diagnostics;

namespace MinimalActorSystem.Benchmarks;

public class Program
{
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
                        var remaining = Interlocked.Decrement(ref counter.Value);
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

    private sealed class ResourceMonitor
    {
        private readonly Process _process = Process.GetCurrentProcess();
        private readonly long _totalAllocated;
        private long _peakMemory;
        private int _maxThreads;

        public ResourceMonitor()
        {
            _process.Refresh();
            _peakMemory = _process.WorkingSet64;
            GC.Collect(2, GCCollectionMode.Forced, true);
            _totalAllocated = GC.GetTotalAllocatedBytes(true);
        }

        public void Snapshot(out long currentMemory, out long peakMemory, out int threadCount, out int pendingThreads, out long allocatedBytes)
        {
            _process.Refresh();
            currentMemory = _process.WorkingSet64;
            if (currentMemory > _peakMemory)
                _peakMemory = currentMemory;
            peakMemory = _peakMemory;
            ThreadPool.GetAvailableThreads(out var availableWorker, out _);
            ThreadPool.GetMaxThreads(out var maxWorker, out _);
            threadCount = maxWorker - availableWorker;
            pendingThreads = _process.Threads.Count;
            if (threadCount > _maxThreads)
                _maxThreads = threadCount;

            allocatedBytes = GC.GetTotalAllocatedBytes(false) - _totalAllocated;
        }

        public void PrintStats(string label, long currentMemory, long peakMemory, int threadCount, int pendingThreads, long allocatedBytes)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {label} ---");
            Console.WriteLine($"  Память текущая:     {currentMemory / 1024.0 / 1024.0:F1} MB");
            Console.WriteLine($"  Память пиковая:     {peakMemory / 1024.0 / 1024.0:F1} MB");
            Console.WriteLine($"  Выделено с начала:  {allocatedBytes / 1024.0 / 1024.0:F1} MB");
            Console.WriteLine($"  Потоков в пуле:     {threadCount}");
            Console.WriteLine($"  Всего потоков:      {pendingThreads}");
            Console.WriteLine($"  Макс потоков пула:  {_maxThreads}");
        }

        public static void PrintGcStats()
        {
            Console.WriteLine();
            Console.WriteLine("--- Сборка мусора ---");
            for (int i = 0; i <= GC.MaxGeneration; i++)
            {
                Console.WriteLine($"  Gen{i}: {GC.CollectionCount(i)} сборок");
            }
            Console.WriteLine($"  Всего выделено: {GC.GetTotalAllocatedBytes(false) / 1024.0 / 1024.0:F1} MB");
        }
    }

    public static async Task Main(string[] _)
    {
        const int pairs = 3000;
        const int messagesPerPair = 5000;

        var monitor = new ResourceMonitor();
        Console.WriteLine($"Тест: {pairs} пар, {messagesPerPair} сообщений на пару");
        Console.WriteLine($"Всего акторов: {pairs * 2}, всего сообщений: {(long)pairs * messagesPerPair * 2:N0}");

        var settings = new Settings { IsProduction = true };
        var system = new ActorSystem(settings);

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
        monitor.Snapshot(out var memAfterCreate, out var peakAfterCreate, out var threadsAfterCreate, out var pendingAfterCreate, out var allocAfterCreate);
        monitor.PrintStats("После создания", memAfterCreate, peakAfterCreate, threadsAfterCreate, pendingAfterCreate, allocAfterCreate);
        Console.WriteLine($"  Время создания: {swCreation.ElapsedMilliseconds} мс");

        Console.WriteLine("\nЗапуск системы...");
        var swStart = Stopwatch.StartNew();
        swStart.Stop();

        monitor.Snapshot(out var memAfterStart, out var peakAfterStart, out var threadsAfterStart, out var pendingAfterStart, out var allocAfterStart);
        monitor.PrintStats("После запуска", memAfterStart, peakAfterStart, threadsAfterStart, pendingAfterStart, allocAfterStart);
        Console.WriteLine($"  Время запуска: {swStart.ElapsedMilliseconds} мс");

        Console.WriteLine($"\nОтправляю стартовые сообщения {pairs} акторам...");
        var swWork = Stopwatch.StartNew();

        for (int i = 0; i < pairs; i++)
        {
            system.Send(startMessages[i]);
        }

        Console.WriteLine("Ожидание завершения...");
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(120));

        swWork.Stop();

        monitor.Snapshot(out var memAfterWork, out var peakAfterWork, out var threadsAfterWork, out var pendingAfterWork, out var allocAfterWork);
        monitor.PrintStats("После нагрузки", memAfterWork, peakAfterWork, threadsAfterWork, pendingAfterWork, allocAfterWork);

        long totalMessages = (long)pairs * messagesPerPair * 2;
        double totalUs = swWork.Elapsed.TotalMicroseconds;
        int totalPingFailed = pingActors.Sum(p => p.SendFailed);
        int totalPongFailed = pongActors.Sum(p => p.SendFailed);

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

        monitor.Snapshot(out var memAfterShutdown, out var peakFinal, out var threadsAfterShutdown, out var pendingAfterShutdown, out var allocAfterShutdown);
        monitor.PrintStats("После завершения", memAfterShutdown, peakFinal, threadsAfterShutdown, pendingAfterShutdown, allocAfterShutdown);
        Console.WriteLine($"  Время завершения: {swShutdown.ElapsedMilliseconds} мс");

        Console.WriteLine();
        Console.WriteLine("Принудительная сборка мусора...");
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        ResourceMonitor.PrintGcStats();

        Console.WriteLine();
        Console.WriteLine("Готово");
    }
}
