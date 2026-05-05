using Microsoft.Extensions.Logging;

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

    private sealed class PingActor(Guid uid, string name, IActorSystem system,
        TaskCompletionSource<bool> done) : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter)
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
            return Task.CompletedTask;
        }
    }

    private sealed class PongActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter)
        {
            if (letter is PingLetter ping)
                System.Send(new PongLetter(Uid, ping.Sender));
            return Task.CompletedTask;
        }
    }

    private sealed class PingPongModelActor(Guid uid, string name, IActorSystem system,
        TaskCompletionSource<bool> done, TaskCompletionSource<bool> modelReady)
        : ModelActor(uid, name, system)
    {
        private PingActor? _ping;
        private Guid _pongUid;

        protected override void BuildModel()
        {
            var pong = new PongActor(Guid.NewGuid(), "pong", System);
            _pongUid = pong.Uid;
            var ping = new PingActor(Guid.NewGuid(), "ping", System, done);

            Create(pong);
            Create(ping);
            _ping = ping;
            ReleaseAll();

            modelReady.TrySetResult(true);
        }

        protected override Task OnModelLetter(Letter letter) => Task.CompletedTask;

        public void StartPing()
        {
            if (_ping != null)
                System.Send(new StartPingLetter(System.Uids.System, _ping.Uid, _pongUid));
        }
    }

    [Fact]
    public async Task PingPongTests_001()
    {
        var settings = new Settings { IsProduction = true };
        var system = new ActorSystem(settings);
        system.SetLogger(new TestOutputLogger(_output));

        var done = new TaskCompletionSource<bool>();
        var modelReady = new TaskCompletionSource<bool>();
        var model = new PingPongModelActor(system.Uids.Model, "pingpong-model", system, done, modelReady);

        system.Start();

        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
        model.StartPing();

        var timeout = Task.Delay(TimeSpan.FromSeconds(1));
        var completed = await Task.WhenAny(done.Task, timeout);
        if (completed == timeout)
            Assert.Fail("Таймаут ожидания Pong");

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Assert.False(system.IsPanic);
    }
}
