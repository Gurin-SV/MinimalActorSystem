using System.Diagnostics;
using System.Linq;
using MinimalActorSystem;

namespace MinimalActorSystem.Benchmarks;

public class Program
{
    private class Counter { public int Value; }

    private sealed class StartMessage(Guid sender, Guid receiver) : Letter(sender, receiver);
    private sealed class PingMessage(Guid sender, Guid receiver) : Letter(sender, receiver);
    private sealed class PongMessage(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class PingActor(Guid uid, string name, IActorSystem system, Guid pongUid, Counter counter, TaskCompletionSource<bool> done, int messagesPerPair)
        : Actor(uid, name, system)
    {
        private int _sent;
        private int _received;

        protected override Task OnLetter(Letter letter)
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
            return Task.CompletedTask;
        }

        private void SendPing()
        {
            _sent++;
            System.Send(new PingMessage(Uid, pongUid));
        }
    }

    private sealed class PongActor(Guid uid, string name, IActorSystem system) : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter)
        {
            if (letter is PingMessage ping)
                System.Send(new PongMessage(Uid, ping.Sender));
            return Task.CompletedTask;
        }
    }

    public static async Task Main(string[] args)
    {
        const int pairs = 3000;
        const int messagesPerPair = 5000;

        var settings = new Settings { IsProduction = true };
        var system = new ActorSystem(settings);

        var allDone = new TaskCompletionSource<bool>();
        var counter = new Counter { Value = pairs };

        Console.WriteLine($"Создаю {pairs * 2} акторов...");
        var swCreation = Stopwatch.StartNew();

        for (int i = 0; i < pairs; i++)
        {
            var pongUid = Guid.NewGuid();
            var pong = new PongActor(pongUid, $"pong-{i}", system);
            var ping = new PingActor(Guid.NewGuid(), $"ping-{i}", system, pongUid, counter, allDone, messagesPerPair);
            system.RegisterActor(pong);
            system.RegisterActor(ping);
        }

        swCreation.Stop();
        Console.WriteLine($"Создано {pairs * 2} акторов за {swCreation.ElapsedMilliseconds} мс");

        Console.WriteLine("Запуск системы...");
        system.Start();

        var sw = Stopwatch.StartNew();

        var pingUids = system.GetAllActors()
            .Where(a => a.Name.StartsWith("ping-"))
            .Select(a => a.Uid)
            .ToList();

        Console.WriteLine($"Отправляю стартовые сообщения {pairs} акторам...");
        foreach (var uid in pingUids)
        {
            system.Send(new StartMessage(Guid.NewGuid(), uid));
        }
        Console.WriteLine("Отправка завершена");

        Console.WriteLine("Ожидание завершения...");
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(120));
        Console.WriteLine("Завершено");

        sw.Stop();

        long totalMessages = (long)pairs * messagesPerPair * 2;
        double totalUs = sw.Elapsed.TotalMicroseconds;

        Console.WriteLine($"Пар: {pairs}, сообщений на пару: {messagesPerPair}");
        Console.WriteLine($"Всего сообщений: {totalMessages:N0}");
        Console.WriteLine($"Время: {sw.ElapsedMilliseconds:N0} мс");
        Console.WriteLine($"На сообщение: {totalUs / totalMessages:F2} мкс");
        Console.WriteLine($"Сообщений/сек: {totalMessages / (sw.ElapsedMilliseconds / 1000.0):N0}");

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Console.WriteLine("Готово");
    }
}
