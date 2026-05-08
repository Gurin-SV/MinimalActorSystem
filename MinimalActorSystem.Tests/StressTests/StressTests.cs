using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;

namespace MinimalActorSystem.Tests.Stress;

public sealed class StressTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private sealed class Counter { public int Value; }

    #region Actors

    private sealed class StressModelActor(IActorSystem system,
        TaskCompletionSource<bool> allDone, TaskCompletionSource<bool> modelReady,
        int pairs, int count)
        : ModelActor(system)
    {
        private readonly List<(PingActor Ping, Guid PongUid)> _list = [];

        protected override void OnBuildModel()
        {
            var counter = new Counter { Value = pairs };

            for (int i = 0; i < pairs; i++)
            {
                var pong = new PongActor(System, Guid.NewGuid(), $"pong-{i}");
                var ping = new PingActor(System, Guid.NewGuid(), $"ping-{i}");
                ping.Init(allDone, counter);

                Create(pong);
                Create(ping);
                _list.Add((ping, pong.Uid));
            }

            modelReady.TrySetResult(true);
        }

        public void StartAll()
        {
            foreach (var (ping, pongUid) in _list)
            {
                ping.Start(pongUid, count);
            }
        }
    }

    private sealed class PingActor : Actor
    {
        private TaskCompletionSource<bool>? _done;
        private Counter? _counter;
        private int _remaining;
        private Guid _pongUid;
        private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(20);
        private readonly TimeoutCallback _retryCallback;

        public PingActor(IActorSystem system, Guid uid, string name) : base(system, uid, name)
        {
            _retryCallback = new TimeoutCallback(uid, 0, RetrySend);
        }

        public void Init(TaskCompletionSource<bool> done, Counter counter)
        {
            _done = done;
            _counter = counter;
        }

        public void Start(Guid pongUid, int count)
        {
            _remaining = count;
            _pongUid = pongUid;
            SendOrRetry(new PingToPongLetter(Uid, _pongUid, _remaining), "initial ping", _retryCallback);
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case PongToPingLetter pong:
                    if (pong.Remaining > 1)
                    {
                        _remaining = pong.Remaining - 1;
                        SendOrRetry(new PingToPongLetter(Uid, pong.Sender, _remaining), "pong reply", _retryCallback);
                    }
                    else
                    {
                        var done = Interlocked.Decrement(ref _counter!.Value);
                        if (done == 0)
                            _done!.TrySetResult(true);
                    }
                    break;
                case TimeServiceLetter timeLetter:
                    timeLetter.Callback.Action();
                    break;
            }
            return default;
        }

        private void SendOrRetry(Letter letter, string context, TimeoutCallback callback)
        {
            if (!System.Send(letter))
            {
                System.Logger.LogInformation("{Name}: queue full, retrying {Context} in {Delay}ms", Name, context, _retryDelay.TotalMilliseconds);
                System.TimeService.Register(_retryDelay, callback);
            }
        }

        private void RetrySend()
        {
            SendOrRetry(new PingToPongLetter(Uid, _pongUid, _remaining), "retry", _retryCallback);
        }
    }

    private sealed class PongActor(IActorSystem system, Guid uid, string name) : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is PingToPongLetter ping)
            {
                if (!System.Send(new PongToPingLetter(Uid, ping.Sender, ping.Remaining > 1 ? ping.Remaining - 1 : 0)))
                {
                    System.Logger.LogInformation("{Name}: queue full sending pong to {Sender}", Name, System.GetActorName(ping.Sender));
                }
            }
            return default;
        }
    }

    private sealed class FakeActor(IActorSystem system, Guid uid, string name)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    private sealed class MassiveActor : Actor
    {
        private int _received;
        private readonly int _target = 100;
        private readonly List<Guid> _targets = [];
        private readonly Counter _counter;
        private readonly TaskCompletionSource<bool> _done;
        private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(20);
        private readonly TimeoutCallback _retryCallback;

        public MassiveActor(IActorSystem system, Guid uid, string name, Counter counter, TaskCompletionSource<bool> done)
            : base(system, uid, name)
        {
            _counter = counter;
            _done = done;
            _retryCallback = new TimeoutCallback(uid, 0, RetrySend);
        }

        public void SetTargets(List<Guid> targets)
        {
            _targets.AddRange(targets);
        }

        public void StartSending()
        {
            foreach (var target in _targets)
            {
                SendOrRetry(new PingMassiveLetter(Uid, target, Uid));
            }
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case PingMassiveLetter ping:
                    if (!System.Send(new PongMassiveLetter(Uid, ping.ReplyTo)))
                    {
                        System.Logger.LogInformation("{Name}: queue full sending pong to {Target}", Name,
                            System.GetActorName(ping.ReplyTo));
                    }
                    break;
                case PongMassiveLetter:
                    var received = Interlocked.Increment(ref _received);
                    if (received >= _target)
                    {
                        var remaining = Interlocked.Decrement(ref _counter.Value);
                        if (remaining == 0)
                            _done.TrySetResult(true);
                    }
                    break;
                case TimeServiceLetter timeLetter:
                    timeLetter.Callback.Action();
                    break;
            }
            return default;
        }

        private void SendOrRetry(PingMassiveLetter letter)
        {
            if (!System.Send(letter))
            {
                System.Logger.LogInformation("{Name}: queue full, retrying in {Delay}ms", Name, _retryDelay.TotalMilliseconds);
                System.TimeService.Register(_retryDelay, _retryCallback);
            }
        }

        private void RetrySend()
        {
            foreach (var target in _targets)
            {
                if (!System.Send(new PingMassiveLetter(Uid, target, Uid)))
                {
                    System.Logger.LogInformation("{Name}: retry still full, scheduling again", Name);
                    System.TimeService.Register(_retryDelay, _retryCallback);
                    return;
                }
            }
        }
    }

    private sealed class MassiveModelActor(IActorSystem system, TaskCompletionSource<bool> allDone,
        TaskCompletionSource<bool> modelReady, int actorCount, int messagesPerActor)
        : ModelActor(system)
    {
        private readonly List<MassiveActor> _actors = [];
        private readonly Counter _counter = new() { Value = actorCount };

        protected override void OnBuildModel()
        {
            for (int i = 0; i < actorCount; i++)
            {
                var actor = new MassiveActor(System, Guid.NewGuid(), $"massive-{i}", _counter, allDone);
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

            modelReady.TrySetResult(true);
        }

        public void StartAll()
        {
            foreach (var actor in _actors)
            {
                actor.StartSending();
            }
        }
    }

    private sealed class RetryTestModelActor(IActorSystem system, TaskCompletionSource<bool> allDone,
        TaskCompletionSource<bool> modelReady, int totalMessages, int queueCapacity)
        : ModelActor(system)
    {
        private FlooderActor? _flooder;

        public int RetryCount => _flooder?.RetryCount ?? 0;

        protected override void OnBuildModel()
        {
            var slow = new SlowActor(System, Guid.NewGuid(), "slow", allDone, totalMessages, queueCapacity);
            _flooder = new FlooderActor(System, Guid.NewGuid(), "flooder", queueCapacity);
            _flooder.Init(slow.Uid, totalMessages);

            Create(slow);
            Create(_flooder);

            modelReady.TrySetResult(true);
        }

        public void StartSending()
        {
            _flooder!.Start();
        }
    }

    private sealed class FlooderActor : Actor
    {
        private Guid _target;
        private int _total;
        private int _currentIndex;
        private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(50);
        private readonly TimeoutCallback _retryCallback;

        public int RetryCount { get; private set; }

        public FlooderActor(IActorSystem system, Guid uid, string name, int queueCapacity)
            : base(system, uid, name, queueCapacity)
        {
            _retryCallback = new TimeoutCallback(uid, 0, RetrySend);
        }

        public void Init(Guid target, int total)
        {
            _target = target;
            _total = total;
        }

        public void Start()
        {
            _currentIndex = 0;
            TrySendCurrent();
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is TimeServiceLetter timeLetter)
            {
                timeLetter.Callback.Action();
            }
            return default;
        }

        private void TrySendCurrent()
        {
            while (_currentIndex < _total)
            {
                if (!System.Send(new TestFloodLetter(Uid, _target, _currentIndex)))
                {
                    RetryCount++;
                    System.Logger.LogInformation("Flooder: queue full for index {Index}, retry #{RetryCount}", _currentIndex, RetryCount);
                    System.TimeService.Register(_retryDelay, _retryCallback);
                    return;
                }
                _currentIndex++;
            }
        }

        private void RetrySend()
        {
            TrySendCurrent();
        }
    }

    private sealed class SlowActor(IActorSystem system, Guid uid, string name, TaskCompletionSource<bool> done,
        int expectedCount, int queueCapacity)
        : Actor(system, uid, name, queueCapacity)
    {
        private readonly TaskCompletionSource<bool> _done = done;
        private readonly int _expectedCount = expectedCount;
        private int _received;

        protected override async ValueTask OnLetter(Letter letter)
        {
            if (letter is TestFloodLetter)
            {
                await Task.Delay(10);
                var received = Interlocked.Increment(ref _received);
                if (received >= _expectedCount)
                    _done.TrySetResult(true);
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

    private sealed class TestFloodLetter(Guid sender, Guid receiver, int index) : Letter(sender, receiver)
    {
        public int Index { get; } = index;
    }

    #endregion Letters

    [Fact]
    public async Task StressTests_001()
    {
        const int pairs = 1;
        const int count = 100_000;
        var system = new ActorSystem(new Settings { IsProduction = true });

        var allDone = new TaskCompletionSource<bool>();
        var modelReady = new TaskCompletionSource<bool>();
        var model = new StressModelActor(system, allDone, modelReady, pairs, count);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

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
        var model = new StressModelActor(system, allDone, modelReady, pairs, count);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

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
            var actor = new FakeActor(system, Guid.NewGuid(), $"actor-{i}");
            system.RegisterActor(actor);
        }
        _output.WriteLine($"Registered: {system.ActorCount}");

        var sw = Stopwatch.StartNew();
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

        var model = new MassiveModelActor(system, allDone, modelReady, actorCount, messagesPerActor);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));
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

    [Fact]
    public async Task StressTests_005()
    {
        const int queueCapacity = 10;
        const int totalMessages = 20;
        var system = new ActorSystem(new Settings
        {
            IsProduction = true
        });
        system.CreateFileLogger("stress_005_retry");
        system.TimeService = new SystemTimeService(system);

        var allDone = new TaskCompletionSource<bool>();
        var modelReady = new TaskCompletionSource<bool>();

        var model = new RetryTestModelActor(system, allDone, modelReady, totalMessages, queueCapacity);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var sw = Stopwatch.StartNew();
        model.StartSending();
        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(30));
        sw.Stop();

        _output.WriteLine($"Time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Total retries: {model.RetryCount}");
        Assert.True(model.RetryCount > 0, "Expected some retries due to small queue");

        system.Shutdown();
        await system.WaitForShutdownAsync();
        Assert.False(system.IsPanic);
    }
}
