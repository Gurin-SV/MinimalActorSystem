namespace MinimalActorSystem.Tests.Core;

public sealed class ActorTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private sealed class TestLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class FakeActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    private sealed class TestActor(Guid uid, string name, IActorSystem system, List<Letter> received)
        : Actor(uid, name, system)
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            received.Add(letter);
            return default;
        }
    }

    private sealed class TestActorWithShutdown(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        public bool ShutdownCalled { get; private set; }

        protected override ValueTask OnLetter(Letter letter) => default;

        protected override ValueTask OnShutdown()
        {
            ShutdownCalled = true;
            return default;
        }
    }

    private sealed class ThrowingActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override ValueTask OnLetter(Letter letter)
            => throw new InvalidOperationException("test error");
    }

    [Fact]
    public void ActorTests_001()
    {
        var uid = Guid.NewGuid();
        var system = SystemFactory.CreateSystem(_output);

        var actor = new FakeActor(uid, "test-actor", system);

        Assert.Equal(uid, actor.Uid);
        Assert.Equal("test-actor", actor.Name);
        Assert.Same(system, actor.System);
    }

    [Fact]
    public void ActorTests_002()
    {
        var system = SystemFactory.CreateSystem(_output);
        var actor = new FakeActor(Guid.NewGuid(), "test", system);
        var letter = new TestLetter(Guid.NewGuid(), actor.Uid);

        var result = actor.TryEnqueue(letter);

        Assert.True(result);
    }

    [Fact]
    public async Task ActorTests_003()
    {
        var system = SystemFactory.CreateSystem(_output);
        var received = new List<Letter>();
        var actor = new TestActor(Guid.NewGuid(), "test", system, received);
        var letter = new TestLetter(Guid.NewGuid(), actor.Uid);

        actor.TryEnqueue(letter);
        var cts = new CancellationTokenSource();
        var task = actor.RunAsync(cts.Token);

        await Task.Delay(1);
        cts.Cancel();
        await task;

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }

    [Fact]
    public async Task ActorTests_004()
    {
        var system = SystemFactory.CreateSystem(_output);
        var actor = new FakeActor(Guid.NewGuid(), "test", system);
        var cts = new CancellationTokenSource();

        cts.Cancel();
        await actor.RunAsync(cts.Token);
    }

    [Fact]
    public async Task ActorTests_005()
    {
        var system = SystemFactory.CreateSystem(_output);
        var actor = new TestActorWithShutdown(Guid.NewGuid(), "test", system);
        system.RegisterActor(actor);

        var cts = new CancellationTokenSource();
        cts.Cancel();
        await actor.RunAsync(cts.Token);

        Assert.True(actor.ShutdownCalled);
        Assert.Equal(0, system.ActorCount);
    }

    [Fact]
    public void ActorTests_006()
    {
        var system = SystemFactory.CreateSystem(_output);
        var received = new List<Letter>();
        var actor = new TestActor(Guid.NewGuid(), "test", system, received);
        var letter = new TestLetter(Guid.NewGuid(), actor.Uid);

        actor.HandleSynchronously(letter);

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }
}
