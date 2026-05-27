namespace MinimalActorSystem.Tests.Core;

public sealed class PingPongTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region TestActors

    private sealed class StartPingLetter(Guid sender, Guid receiver, Guid pongUid)
        : Letter(sender, receiver)
    {
        public Guid PongUid { get; } = pongUid;
    }

    private sealed class PingLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class PongLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class PingActor(IActorSystem system, Guid uid, string name,
        TaskCompletionSource<bool> done)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case StartPingLetter start:
                    System.Send(new PingLetter(Uid, start.PongUid));
                    break;
                case PongLetter:
                    done.TrySetResult(true);
                    break;
            }
            return default;
        }
    }

    private sealed class PongActor(IActorSystem system, Guid uid, string name)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is PingLetter ping)
                System.Send(new PongLetter(Uid, ping.Sender));
            return default;
        }
    }

    private sealed class PingPongModelActor(IActorSystem system, TaskCompletionSource<bool> done,
        TaskCompletionSource<bool> modelReady)
        : ModelActor(system)
    {
        private PingActor? _ping;
        private Guid _pongUid;

        protected override void OnBuildModel()
        {
            PongActor pong = new(System, Guid.NewGuid(), "pong");
            _pongUid = pong.Uid;
            PingActor ping = new(System, Guid.NewGuid(), "ping", done);

            Create(pong);
            Create(ping);
            _ping = ping;

            modelReady.TrySetResult(true);
        }

        public void StartPing()
        {
            if (_ping != null)
                System.Send(new StartPingLetter(SystemUids.System, _ping.Uid, _pongUid));
        }
    }

    private sealed class MultiExchangePingActor(IActorSystem system, Guid uid, string name,
        Guid pongUid, int expectedExchanges, TaskCompletionSource<bool> done)
        : Actor(system, uid, name)
    {
        private int _pongCount;
        private readonly int _expectedExchanges = expectedExchanges;
        private readonly Guid _pongUid = pongUid;
        private readonly TaskCompletionSource<bool> _done = done;

        protected override ValueTask OnLetter(Letter letter)
        {
            switch (letter)
            {
                case StartPingLetter:
                    System.Send(new PingLetter(Uid, _pongUid));
                    break;
                case PongLetter:
                    _pongCount++;
                    if (_pongCount < _expectedExchanges)
                    {
                        System.Send(new PingLetter(Uid, _pongUid));
                    }
                    else
                    {
                        _done.TrySetResult(true);
                    }
                    break;
            }
            return default;
        }
    }

    private sealed class MultiExchangePongActor(IActorSystem system, Guid uid, string name)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is PingLetter ping)
            {
                System.Send(new PongLetter(Uid, ping.Sender));
            }
            return default;
        }
    }

    #endregion

    #region Tests

    /// <summary>
    /// Базовый сценарий: PingActor отправляет Ping, PongActor отвечает Pong.
    /// Проверяется асинхронная доставка сообщений между акторами.
    /// </summary>
    [Fact]
    public async Task PingPongTests_001()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        TaskCompletionSource<bool> done = new();
        TaskCompletionSource<bool> modelReady = new();
        PingPongModelActor model = new(system, done, modelReady);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
        model.StartPing();

        Task timeout = Task.Delay(TimeSpan.FromSeconds(1));
        Task completed = await Task.WhenAny(done.Task, timeout);
        if (completed == timeout)
            Assert.Fail("Timeout waiting for Pong");

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Assert.False(system.IsPanic);
    }

    /// <summary>
    /// Синхронный режим (TimeServiceModes.Sync): проверка, что пинг-понг работает
    /// без асинхронных задержек. Все сообщения обрабатываются в потоке отправителя.
    /// </summary>
    [Fact]
    public async Task PingPongTests_002()
    {
        Settings settings = new()
        {
            TimeServiceModes = TimeServiceModes.Sync
        };
        ActorSystem system = SystemFactory.CreateSystem(_output, settings);

        TaskCompletionSource<bool> done = new();
        TaskCompletionSource<bool> modelReady = new();
        PingPongModelActor model = new(system, done, modelReady);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

        Assert.True(modelReady.Task.IsCompleted, "Model should be ready");

        model.StartPing();

        Assert.True(done.Task.IsCompleted, "Pong should be received");

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Assert.False(system.IsPanic);
    }

    /// <summary>
    /// Проверка, что пинг-понг работает в асинхронном режиме (TimeServiceModes.Async)
    /// </summary>
    [Fact]
    public async Task PingPongTests_003()
    {
        Settings settings = new()
        {
            TimeServiceModes = TimeServiceModes.Async
        };
        ActorSystem system = SystemFactory.CreateSystem(_output, settings);

        TaskCompletionSource<bool> done = new();
        TaskCompletionSource<bool> modelReady = new();
        PingPongModelActor model = new(system, done, modelReady);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
        model.StartPing();

        Task timeout = Task.Delay(TimeSpan.FromSeconds(1));
        Task completed = await Task.WhenAny(done.Task, timeout);
        if (completed == timeout)
            Assert.Fail("Timeout waiting for Pong");

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Assert.False(system.IsPanic);
    }

    /// <summary>
    /// Проверка корректной остановки системы после успешного пинг-понга.
    /// Все акторы должны корректно завершиться.
    /// </summary>
    [Fact]
    public async Task PingPongTests_004()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        TaskCompletionSource<bool> done = new();
        TaskCompletionSource<bool> modelReady = new();
        PingPongModelActor model = new(system, done, modelReady);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
        model.StartPing();

        Task timeout = Task.Delay(TimeSpan.FromSeconds(1));
        Task completed = await Task.WhenAny(done.Task, timeout);
        if (completed == timeout)
            Assert.Fail("Timeout waiting for Pong");

        system.Shutdown();
        Task shutdownTask = system.WaitForShutdownAsync();
        Task shutdownTimeout = Task.Delay(TimeSpan.FromSeconds(2));
        Task shutdownCompleted = await Task.WhenAny(shutdownTask, shutdownTimeout);

        Assert.Equal(shutdownTask, shutdownCompleted);
        Assert.False(system.IsPanic);
    }

    /// <summary>
    /// Проверка, что система не входит в состояние Panic при нормальной работе
    /// </summary>
    [Fact]
    public async Task PingPongTests_005()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        TaskCompletionSource<bool> done = new();
        TaskCompletionSource<bool> modelReady = new();
        PingPongModelActor model = new(system, done, modelReady);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
        model.StartPing();

        Task timeout = Task.Delay(TimeSpan.FromSeconds(1));
        Task completed = await Task.WhenAny(done.Task, timeout);
        if (completed == timeout)
            Assert.Fail("Timeout waiting for Pong");

        Assert.False(system.IsPanic);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Проверка многократного пинг-понга (несколько обменов)
    /// </summary>
    [Fact]
    public async Task PingPongTests_006()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        const int expectedExchanges = 5;
        TaskCompletionSource<bool> done = new();

        Guid pongUid = Guid.NewGuid();
        Guid pingUid = Guid.NewGuid();

        MultiExchangePongActor pong = new(system, pongUid, "multi-pong");
        MultiExchangePingActor ping = new(system, pingUid, "multi-ping", pongUid, expectedExchanges, done);

        system.RegisterActor(pong);
        system.RegisterActor(ping);

        system.Send(new StartPingLetter(SystemUids.System, pingUid, pongUid));

        Task timeout = Task.Delay(TimeSpan.FromSeconds(2));
        Task completed = await Task.WhenAny(done.Task, timeout);
        if (completed == timeout)
            Assert.Fail("Timeout waiting for multiple ping-pong exchanges");

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Assert.False(system.IsPanic);
    }

    #endregion
}
