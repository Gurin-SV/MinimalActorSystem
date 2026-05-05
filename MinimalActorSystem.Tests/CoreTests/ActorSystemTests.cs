namespace MinimalActorSystem.Tests.Core;

public sealed class ActorSystemTests
{
    private sealed class FakeActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter) => Task.CompletedTask;
    }

    private sealed class TestLetter(Guid sender, Guid receiver) : Letter(sender, receiver) { }

    private sealed class TestActor(
        Guid uid,
        string name,
        IActorSystem system,
        List<Letter> received,
        TaskCompletionSource<bool>? tcs = null) : Actor(uid, name, system)
    {
        private readonly List<Letter> _received = received;
        private readonly TaskCompletionSource<bool>? _tcs = tcs;

        protected override Task OnLetter(Letter letter)
        {
            _received.Add(letter);
            _tcs?.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    private sealed class TestActorWithShutdown(Guid uid, string name, IActorSystem system,
        TaskCompletionSource<bool> shutdownTcs)
        : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter) => Task.CompletedTask;

        protected override Task OnShutdown()
        {
            shutdownTcs.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    private sealed class TestModelActor(Guid uid, string name, IActorSystem system, bool callBuildModel = true)
    : ModelActor(uid, name, system)
    {
        public bool BuildModelCalled { get; private set; }

        protected override void BuildModel()
        {
            BuildModelCalled = true;
            if (callBuildModel)
            {
                var child = new FakeActor(Guid.NewGuid(), "child", System);
                Create(child);
                ReleaseAll();
            }
        }

        protected override Task OnModelLetter(Letter letter) => Task.CompletedTask;
    }

    // Проверка: конструктор устанавливает Settings, Uids
    [Fact]
    public void ActorSystemTests_001()
    {
        var settings = new Settings();

        var system = new ActorSystem(settings);

        Assert.Same(settings, system.Settings);
        Assert.NotNull(system.Uids);
    }

    // Проверка: RegisterActor увеличивает ActorCount
    [Fact]
    public void ActorSystemTests_002()
    {
        var system = new ActorSystem(new Settings());
        var actor = new FakeActor(Guid.NewGuid(), "test", system);

        system.RegisterActor(actor);

        Assert.Equal(1, system.ActorCount);
    }

    // Проверка: WaitForShutdownAsync завершается при опустошении реестра
    [Fact]
    public async Task ActorSystemTests_003()
    {
        var system = new ActorSystem(new Settings());
        var actor = new FakeActor(Guid.NewGuid(), "test", system);
        system.RegisterActor(actor);

        system.UnregisterActor(actor.Uid);

        await system.WaitForShutdownAsync();
    }

    // Проверка: Send доставляет письмо в очередь (асинхронный режим)
    [Fact]
    public async Task ActorSystemTests_004()
    {
        var system = new ActorSystem(new Settings());
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var actor = new TestActor(Guid.NewGuid(), "test", system, received, tcs);
        system.RegisterActor(actor);
        system.Start();
        var letter = new TestLetter(system.Uids.System, actor.Uid);

        system.Send(letter);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Single(received);
        Assert.Same(letter, received[0]);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    // Проверка: Send не падает, если получатель не найден
    [Fact]
    public void ActorSystemTests_005()
    {
        var system = new ActorSystem(new Settings());

        system.Send(new TestLetter(system.Uids.System, Guid.NewGuid()));
    }

    // Проверка: Send в синхронном режиме обрабатывает письмо сразу
    [Fact]
    public void ActorSystemTests_006()
    {
        var settings = new Settings { SynchronousProcessing = true };
        var system = new ActorSystem(settings);
        var received = new List<Letter>();
        var actor = new TestActor(Guid.NewGuid(), "test", system, received);
        system.RegisterActor(actor);
        system.Start();
        var letter = new TestLetter(system.Uids.System, actor.Uid);

        system.Send(letter);

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }

    // Проверка: Start запускает циклы акторов — ModelActor создаёт прикладных
    [Fact]
    public void ActorSystemTests_007()
    {
        var system = new ActorSystem(new Settings());
        var modelActor = new TestModelActor(system.Uids.Model, "model", system);

        system.Start();

        Assert.True(modelActor.BuildModelCalled);
        Assert.Equal(2, system.ActorCount); // сам ModelActor + 1 прикладной из BuildModel
    }

    [Fact]
    public async Task ActorSystemTests_008()
    {
        var system = new ActorSystem(new Settings());
        var shutdownTcs = new TaskCompletionSource<bool>();
        var actor = new TestActorWithShutdown(Guid.NewGuid(), "test", system, shutdownTcs);
        system.RegisterActor(actor);
        system.Start();

        system.Shutdown();

        await shutdownTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await system.WaitForShutdownAsync();
    }

    [Fact]
    public async Task ActorSystemTests_009()
    {
        var system = new ActorSystem(new Settings());
        var shutdownTcs = new TaskCompletionSource<bool>();
        var actor = new TestActorWithShutdown(Guid.NewGuid(), "test", system, shutdownTcs);
        system.RegisterActor(actor);
        system.Start();

        system.Panic();

        Assert.True(system.IsPanic);
        await shutdownTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await system.WaitForShutdownAsync();
    }
}
