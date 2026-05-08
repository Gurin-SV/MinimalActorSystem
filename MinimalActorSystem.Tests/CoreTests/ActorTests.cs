namespace MinimalActorSystem.Tests.Core;

public sealed class ActorTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private sealed class TestLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class FakeActor(IActorSystem system, Guid uid, string name)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    private sealed class TestActor(IActorSystem system, Guid uid, string name, List<Letter> received)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            received.Add(letter);
            return default;
        }
    }

    private sealed class TestActorWithShutdown(IActorSystem system, Guid uid, string name)
        : Actor(system, uid, name)
    {
        public bool ShutdownCalled { get; private set; }

        protected override ValueTask OnLetter(Letter letter) => default;

        protected override ValueTask OnShutdown()
        {
            ShutdownCalled = true;
            return default;
        }
    }

    private sealed class ThrowingActor(IActorSystem system, Guid uid, string name)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter)
            => throw new InvalidOperationException("test error");
    }

    [Fact]
    public void ActorTests_001()
    {
        var uid = Guid.NewGuid();
        var system = SystemFactory.CreateSystem(_output);

        var actor = new FakeActor(system, uid, "test-actor");

        Assert.Equal(uid, actor.Uid);
        Assert.Equal("test-actor", actor.Name);
        Assert.Same(system, actor.System);
    }

    [Fact]
    public void ActorTests_002()
    {
        var system = SystemFactory.CreateSystem(_output);
        var actor = new FakeActor(system, Guid.NewGuid(), "test");
        var letter = new TestLetter(Guid.NewGuid(), actor.Uid);

        var result = actor.TryEnqueue(letter);

        Assert.True(result);
    }

    [Fact]
    public async Task ActorTests_003()
    {
        var system = SystemFactory.CreateSystem(_output);
        var received = new List<Letter>();
        var actor = new TestActor(system, Guid.NewGuid(), "test", received);
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
        var actor = new FakeActor(system, Guid.NewGuid(), "test");
        var cts = new CancellationTokenSource();

        cts.Cancel();
        await actor.RunAsync(cts.Token);
    }

    [Fact]
    public async Task ActorTests_005()
    {
        var system = SystemFactory.CreateSystem(_output);
        var actor = new TestActorWithShutdown(system, Guid.NewGuid(), "test");
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
        var actor = new TestActor(system, Guid.NewGuid(), "test", received);
        var letter = new TestLetter(Guid.NewGuid(), actor.Uid);

        actor.HandleSynchronously(letter);

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }
}
