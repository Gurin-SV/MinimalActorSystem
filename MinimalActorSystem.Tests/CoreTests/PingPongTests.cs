namespace MinimalActorSystem.Tests.Core;

public sealed class PingPongTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

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
            var pong = new PongActor(System, Guid.NewGuid(), "pong");
            _pongUid = pong.Uid;
            var ping = new PingActor(System, Guid.NewGuid(), "ping", done);

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

    [Fact]
    public async Task PingPongTests_001()
    {
        var system = SystemFactory.CreateSystem(_output);

        var done = new TaskCompletionSource<bool>();
        var modelReady = new TaskCompletionSource<bool>();
        var model = new PingPongModelActor(system, done, modelReady);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
        model.StartPing();

        var timeout = Task.Delay(TimeSpan.FromSeconds(1));
        var completed = await Task.WhenAny(done.Task, timeout);
        if (completed == timeout)
            Assert.Fail("Timeout waiting for Pong");

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Assert.False(system.IsPanic);
    }

    [Fact]
    public async Task PingPongTests_002_Debug()
    {
        var settings = new Settings
        {
            IsProduction = false,
            SynchronousProcessing = true
        };
        var system = SystemFactory.CreateSystem(_output, settings);

        var done = new TaskCompletionSource<bool>();
        var modelReady = new TaskCompletionSource<bool>();
        var model = new PingPongModelActor(system, done, modelReady);
        system.RegisterActor(model);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));

        Assert.True(modelReady.Task.IsCompleted, "Model should be ready");

        model.StartPing();

        Assert.True(done.Task.IsCompleted, "Pong should be received");

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Assert.False(system.IsPanic);
    }
}
