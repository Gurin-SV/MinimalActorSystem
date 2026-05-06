using System.Diagnostics;

namespace MinimalActorSystem.Tests.Stress;

public sealed class StressTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private sealed class Counter { public int Value; }

    #region Actors
    private sealed class StressModelActor(Guid uid, string name, IActorSystem system,
        TaskCompletionSource<bool> allDone, TaskCompletionSource<bool> modelReady,
        int pairs, int count)
        : ModelActor(uid, name, system)
    {
        private readonly List<(PingActor Ping, Guid PongUid)> _list = [];

        protected override void BuildModel()
        {
            var counter = new Counter { Value = pairs };

            for (int i = 0; i < pairs; i++)
            {
                var pong = new PongActor(Guid.NewGuid(), $"pong-{i}", System);
                var ping = new PingActor(Guid.NewGuid(), $"ping-{i}", System);
                ping.Init(allDone, counter);

                Create(pong);
                Create(ping);
                _list.Add((ping, pong.Uid));
            }

            ReleaseAll();
            modelReady.TrySetResult(true);
        }

        protected override ValueTask OnModelLetter(Letter letter) => default;

        public void StartAll()
        {
            foreach (var (ping, pongUid) in _list)
            {
                System.Send(new StartPairLetter(System.Uids.System, ping.Uid, pongUid, count));
            }
        }
    }

    private sealed class PingActor(Guid uid, string name, IActorSystem system) : Actor(uid, name, system)
    {
        private TaskCompletionSource<bool>? _done;
        private Counter? _counter;

        public void Init(TaskCompletionSource<bool> done, Counter counter)
        {
            _done = done;
            _counter = counter;
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case StartPairLetter start:
                    System.Send(new PingToPongLetter(Uid, start.PongUid, start.Remaining));
                    break;
                case PongToPingLetter pong:
                    if (pong.Remaining > 1)
                        System.Send(new PingToPongLetter(Uid, pong.Sender, pong.Remaining - 1));
                    else
                    {
                        var done = Interlocked.Decrement(ref _counter!.Value);
                        if (done == 0)
                            _done!.TrySetResult(true);
                    }
                    break;
            }
            return default;
        }
    }

    private sealed class PongActor(Guid uid, string name, IActorSystem system) : Actor(uid, name, system)
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is PingToPongLetter ping)
            {
                System.Send(new PongToPingLetter(Uid, ping.Sender, ping.Remaining > 1 ? ping.Remaining - 1 : 0));
            }
            return default;
        }
    }

    private sealed class FakeActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    private sealed class MassiveActor(Guid uid, string name, IActorSystem system, Counter counter, TaskCompletionSource<bool> done)
        : Actor(uid, name, system)
    {
        private int _received;
        private readonly int _target = 100;
        private readonly List<Guid> _targets = [];

        public void SetTargets(List<Guid> targets)
        {
            _targets.AddRange(targets);
        }

        public void StartSending()
        {
            foreach (var target in _targets)
            {
                System.Send(new PingMassiveLetter(Uid, target, Uid));
            }
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case PingMassiveLetter ping:
                    System.Send(new PongMassiveLetter(Uid, ping.Sender));
                    break;
                case PongMassiveLetter:
                    var received = Interlocked.Increment(ref _received);
                    if (received >= _target)
                    {
                        var remaining = Interlocked.Decrement(ref counter.Value);
                        if (remaining == 0)
                            done.TrySetResult(true);
                    }
                    break;
            }
            return default;
        }
    }

    private sealed class MassiveModelActor(
        Guid uid, string name, IActorSystem system,
        TaskCompletionSource<bool> allDone, TaskCompletionSource<bool> modelReady,
        int actorCount, int messagesPerActor)
        : ModelActor(uid, name, system)
    {
        private readonly List<MassiveActor> _actors = [];
        private readonly Counter _counter = new() { Value = actorCount };

        protected override void BuildModel()
        {
            for (int i = 0; i < actorCount; i++)
            {
                var actor = new MassiveActor(Guid.NewGuid(), $"massive-{i}", System, _counter, allDone);
                Create(actor);
                _actors.Add(actor);
            }

            var rng = new Random(42);
            var allUids = _actors.Select(a => a.Uid).ToList();

            foreach (var actor in _actors)
            {
                var targets = new List<Guid>(messagesPerActor);
                for (int j = 0; j < messagesPerActor; j++)
                {
                    var target = allUids[rng.Next(allUids.Count)];
                    targets.Add(target);
                }
                actor.SetTargets(targets);
            }

            ReleaseAll();
            modelReady.TrySetResult(true);
        }

        protected override ValueTask OnModelLetter(Letter letter) => default;

        public void StartAll()
        {
            foreach (var actor in _actors)
            {
                actor.StartSending();
            }
        }
    }

    #endregion Actors

    #region Letters

    private sealed class PingToPongLetter(Guid sender, Guid receiver, int remaining) : Letter(sender, receiver)
    {
        public int Remaining { get; } = remaining;
    }

    private sealed class PongToPingLetter(Guid sender, Guid receiver, int remaining) : Letter(sender, receiver)
    {
        public int Remaining { get; } = remaining;
    }

    private sealed class StartPairLetter(Guid sender, Guid receiver, Guid pongUid, int remaining) : Letter(sender, receiver)
    {
        public Guid PongUid { get; } = pongUid;
        public int Remaining { get; } = remaining;
    }

    private sealed class PingMassiveLetter(Guid sender, Guid receiver, Guid replyTo) : Letter(sender, receiver)
    {
        public Guid ReplyTo { get; } = replyTo;
    }

    private sealed class PongMassiveLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    #endregion Letters

    [Fact]
    public async Task StressTests_001()
    {
        const int pairs = 1;
        const int count = 100_000;
        var system = new ActorSystem(new Settings { IsProduction = true });

        var allDone = new TaskCompletionSource<bool>();
        var modelReady = new TaskCompletionSource<bool>();
        var model = new StressModelActor(system.Uids.Model, "model", system, allDone, modelReady, pairs, count);

        system.Start();
        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var sw = Stopwatch.StartNew();
        model.StartAll();
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(60));
        sw.Stop();

        int totalMessages = pairs * count * 2;
        double totalUs = sw.Elapsed.TotalMicroseconds;
        double usPerMessage = totalUs / totalMessages;
        double usPerExchange = totalUs / (pairs * count);

        _output.WriteLine($"Pairs: {pairs}, exchanges: {count}");
        _output.WriteLine($"Total messages: {totalMessages}");
        _output.WriteLine($"Time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Per message: {usPerMessage:F2} us");
        _output.WriteLine($"Per exchange: {usPerExchange:F2} us");
        _output.WriteLine($"Messages/sec: {totalMessages / (sw.ElapsedMilliseconds / 1000.0):F0}");

        system.Shutdown();
        await system.WaitForShutdownAsync();
        Assert.False(system.IsPanic);
    }

    [Fact]
    public async Task StressTests_002()
    {
        const int pairs = 300;
        const int count = 1_000;
        var system = new ActorSystem(new Settings { IsProduction = true });

        var allDone = new TaskCompletionSource<bool>();
        var modelReady = new TaskCompletionSource<bool>();
        var model = new StressModelActor(system.Uids.Model, "model", system, allDone, modelReady, pairs, count);

        system.Start();
        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var sw = Stopwatch.StartNew();
        model.StartAll();
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(60));
        sw.Stop();

        int totalMessages = pairs * count * 2;
        double totalUs = sw.Elapsed.TotalMicroseconds;
        double usPerMessage = totalUs / totalMessages;

        _output.WriteLine($"Pairs: {pairs}, exchanges per pair: {count}");
        _output.WriteLine($"Total messages: {totalMessages}");
        _output.WriteLine($"Time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Per message: {usPerMessage:F2} us");
        _output.WriteLine($"Messages/sec: {totalMessages / (sw.ElapsedMilliseconds / 1000.0):F0}");

        system.Shutdown();
        await system.WaitForShutdownAsync();
        Assert.False(system.IsPanic);
    }

    [Fact]
    public async Task StressTests_003()
    {
        const int actorCount = 10000;
        var system = new ActorSystem(new Settings { IsProduction = true });

        for (int i = 0; i < actorCount; i++)
        {
            var actor = new FakeActor(Guid.NewGuid(), $"actor-{i}", system);
            system.RegisterActor(actor);
        }
        _output.WriteLine($"Registered: {system.ActorCount}");

        var sw = Stopwatch.StartNew();
        system.Start();
        _output.WriteLine($"Start: {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        system.Shutdown();
        await system.WaitForShutdownAsync();
        _output.WriteLine($"Shutdown+Wait: {sw.ElapsedMilliseconds} ms");

        _output.WriteLine("Done");
    }

    [Fact]
    public async Task StressTests_004()
    {
        const int actorCount = 2_000;
        const int messagesPerActor = 100;
        var system = new ActorSystem(new Settings { IsProduction = true });

        var allDone = new TaskCompletionSource<bool>();
        var modelReady = new TaskCompletionSource<bool>();

        var model = new MassiveModelActor(
            system.Uids.Model, "model", system,
            allDone, modelReady,
            actorCount, messagesPerActor);

        system.Start();
        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var sw = Stopwatch.StartNew();
        model.StartAll();
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(120));
        sw.Stop();

        int totalMessages = actorCount * messagesPerActor;
        double totalUs = sw.Elapsed.TotalMicroseconds;
        double usPerMessage = totalUs / totalMessages;
        double avgLatencyUs = totalUs / (actorCount * messagesPerActor);

        _output.WriteLine($"Actors: {actorCount}");
        _output.WriteLine($"Messages per actor: {messagesPerActor}");
        _output.WriteLine($"Total messages: {totalMessages}");
        _output.WriteLine($"Time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Per message: {usPerMessage:F2} us");
        _output.WriteLine($"Messages/sec: {totalMessages / (sw.ElapsedMilliseconds / 1000.0):F0}");
        _output.WriteLine($"Latency: {avgLatencyUs:F2} us");

        system.Shutdown();
        await system.WaitForShutdownAsync();
        Assert.False(system.IsPanic);
    }
}
