namespace MinimalActorSystem.Tests.Core;

public sealed class ActorSystemTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private sealed class FakeActor(IActorSystem system, Guid uid, string name)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    private sealed class TestLetter(Guid sender, Guid receiver)
        : Letter(sender, receiver)
    {
    }

    private sealed class TestActor(IActorSystem system, Guid uid, string name,  List<Letter> received,
        TaskCompletionSource<bool>? tcs = null)
        : Actor(system, uid, name)
    {
        private readonly List<Letter> _received = received;
        private readonly TaskCompletionSource<bool>? _tcs = tcs;

        protected override ValueTask OnLetter(Letter letter)
        {
            _received.Add(letter);
            _tcs?.TrySetResult(true);
            return default;
        }
    }

    private sealed class TestActorWithShutdown(IActorSystem system, Guid uid, string name, 
        TaskCompletionSource<bool> shutdownTcs)
        : Actor(system, uid, name)
    {
        protected override ValueTask OnLetter(Letter letter) => default;

        protected override ValueTask OnShutdown()
        {
            shutdownTcs.TrySetResult(true);
            return default;
        }
    }

    private sealed class TestModelActor(IActorSystem system, TaskCompletionSource<bool> modelReady)
        : ModelActor(system)
    {
        protected override void OnBuildModel()
        {
            Create(new FakeActor(System, Guid.NewGuid(), "child1"));
            Create(new FakeActor(System, Guid.NewGuid(), "child2"));
            modelReady.TrySetResult(true);
        }
    }

    [Fact]
    public void ActorSystemTests_001()
    {
        var settings = new Settings();
        var system = SystemFactory.CreateSystem(_output, settings);

        Assert.Same(settings, system.Settings);
    }

    [Fact]
    public void ActorSystemTests_002()
    {
        var system = SystemFactory.CreateSystem(_output);
        system.RegisterActor(new FakeActor(system, Guid.NewGuid(), "test"));

        Assert.Equal(1, system.ActorCount);
    }

    [Fact]
    public async Task ActorSystemTests_003()
    {
        var system = SystemFactory.CreateSystem(_output);
        Guid uid = Guid.NewGuid();
        system.RegisterActor(new FakeActor(system, uid, "test"));
        system.UnregisterActor(uid);
        await system.WaitForShutdownAsync();

        Assert.Equal(0, system.ActorCount);
    }

    [Fact]
    public async Task ActorSystemTests_004()
    {
        var system = SystemFactory.CreateSystem(_output);
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var actor = new TestActor(system, Guid.NewGuid(), "test", received, tcs);
        system.RegisterActor(actor);
        var letter = new TestLetter(SystemUids.System, actor.Uid);

        system.Send(letter);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Single(received);
        Assert.Same(letter, received[0]);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    [Fact]
    public void ActorSystemTests_005()
    {
        var system = SystemFactory.CreateSystem(_output);

        system.Send(new TestLetter(SystemUids.System, Guid.NewGuid()));
    }

    [Fact]
    public void ActorSystemTests_006()
    {
        var settings = new Settings { SynchronousProcessing = true };
        var system = SystemFactory.CreateSystem(_output, settings);
        var received = new List<Letter>();
        var actor = new TestActor(system, Guid.NewGuid(), "test", received);
        system.RegisterActor(actor);
        var letter = new TestLetter(SystemUids.System, actor.Uid);

        system.Send(letter);

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }

    [Fact]
    public async Task ActorSystemTests_007()
    {
        var system = SystemFactory.CreateSystem(_output);
        var modelReady = new TaskCompletionSource<bool>();
        var modelActor = new TestModelActor(system, modelReady);
        system.RegisterActor(modelActor);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));
        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, system.ActorCount);
    }

    [Fact]
    public async Task ActorSystemTests_008()
    {
        var system = SystemFactory.CreateSystem(_output);
        var shutdownTcs = new TaskCompletionSource<bool>();
        var actor = new TestActorWithShutdown(system, Guid.NewGuid(), "test", shutdownTcs);
        system.RegisterActor(actor);

        system.Shutdown();

        await shutdownTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await system.WaitForShutdownAsync();
    }

    [Fact]
    public async Task ActorSystemTests_009()
    {
        var system = SystemFactory.CreateSystem(_output);
        var shutdownTcs = new TaskCompletionSource<bool>();
        var actor = new TestActorWithShutdown(system, Guid.NewGuid(), "test", shutdownTcs);
        system.RegisterActor(actor);

        system.Panic();

        Assert.True(system.IsPanic);
        await shutdownTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await system.WaitForShutdownAsync();
    }
}
