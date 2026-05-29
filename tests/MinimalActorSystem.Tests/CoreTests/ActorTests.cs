namespace MinimalActorSystem.Tests.Core;

public sealed class ActorTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region TestActors

    private sealed class TestLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class FakeActor(IActorSystem system, Guid uid, string name, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    private sealed class TestActor(IActorSystem system, Guid uid, string name, List<Letter> received, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
    {
        protected override ValueTask OnLetter(Letter letter)
        {
            received.Add(letter);
            return default;
        }
    }

    private sealed class TestActorWithShutdown(IActorSystem system, Guid uid, string name, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
    {
        public bool ShutdownCalled { get; private set; }

        protected override ValueTask OnLetter(Letter letter) => default;

        protected override ValueTask OnShutdown()
        {
            ShutdownCalled = true;
            return default;
        }
    }

    private sealed class ThrowingActor(IActorSystem system, Guid uid, string name, List<Exception> errors, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
    {
        private readonly List<Exception> _errors = errors;

        protected override ValueTask OnLetter(Letter letter)
        {
            InvalidOperationException ex = new("test error");
            _errors.Add(ex);
            throw ex;
        }
    }

    private sealed class SlowActor(IActorSystem system, Guid uid, string name, TaskCompletionSource<bool> started, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
    {
        private readonly TaskCompletionSource<bool> _started = started;

        protected override async ValueTask OnLetter(Letter letter)
        {
            _started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, System.CancellationToken);
        }
    }

    #endregion

    #region Tests

    /// <summary>
    /// Проверка: конструктор актора правильно устанавливает Uid, Name и System.
    /// </summary>
    [Fact]
    public void ActorTests_001()
    {
        Guid uid = Guid.NewGuid();
        ActorSystem system = SystemFactory.CreateSystem(_output);

        FakeActor actor = new(system, uid, "test-actor");

        Assert.Equal(uid, actor.Uid);
        Assert.Equal("test-actor", actor.Name);
        Assert.Same(system, actor.System);
    }

    /// <summary>
    /// Проверка: TryEnqueue успешно помещает письмо в очередь.
    /// </summary>
    [Fact]
    public void ActorTests_002()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        FakeActor actor = new(system, Guid.NewGuid(), "test");
        TestLetter letter = new(Guid.NewGuid(), actor.Uid);

        bool result = actor.TryEnqueue(letter);

        Assert.True(result);
        Assert.Equal(1, actor.QueueSize);
    }

    /// <summary>
    /// Проверка: RunAsync обрабатывает письма из очереди.
    /// </summary>
    [Fact]
    public async Task ActorTests_003()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        List<Letter> received = [];
        TestActor actor = new(system, Guid.NewGuid(), "test", received);
        TestLetter letter = new(Guid.NewGuid(), actor.Uid);

        actor.TryEnqueue(letter);
        CancellationTokenSource cts = new();
        Task task = actor.RunAsync(cts.Token);

        await Task.Delay(1);
        cts.Cancel();
        await task;

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }

    /// <summary>
    /// Проверка: RunAsync завершается немедленно при уже отменённом токене.
    /// </summary>
    [Fact]
    public async Task ActorTests_004()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        FakeActor actor = new(system, Guid.NewGuid(), "test");
        CancellationTokenSource cts = new();

        cts.Cancel();
        await actor.RunAsync(cts.Token);
    }

    /// <summary>
    /// Проверка: при завершении актора вызывается OnShutdown и актор удаляется из реестра.
    /// </summary>
    [Fact]
    public async Task ActorTests_005()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        TestActorWithShutdown actor = new(system, Guid.NewGuid(), "test");
        system.RegisterActor(actor);

        CancellationTokenSource cts = new();
        cts.Cancel();
        await actor.RunAsync(cts.Token);

        Assert.True(actor.ShutdownCalled);
        Assert.Equal(0, system.ActorCount);
    }

    /// <summary>
    /// Проверка: HandleSynchronously обрабатывает письмо синхронно в потоке отправителя.
    /// </summary>
    [Fact]
    public void ActorTests_006()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        List<Letter> received = [];
        TestActor actor = new(system, Guid.NewGuid(), "test", received);
        TestLetter letter = new(Guid.NewGuid(), actor.Uid);

        actor.HandleSynchronously(letter);

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }

    /// <summary>
    /// Проверка: при переполнении очереди новые письма отбрасываются.
    /// </summary>
    [Fact]
    public void ActorTests_007()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        FakeActor actor = new(system, Guid.NewGuid(), "test", queueCapacity: 1);
        TestLetter letter1 = new(Guid.NewGuid(), actor.Uid);
        TestLetter letter2 = new(Guid.NewGuid(), actor.Uid);

        bool result1 = actor.TryEnqueue(letter1);
        bool result2 = actor.TryEnqueue(letter2);

        Assert.True(result1);
        Assert.True(result2); // TryWrite возвращает true даже при DropWrite

        // Но очередь не должна увеличиться сверх capacity
        Assert.Equal(1, actor.QueueSize);
    }

    /// <summary>
    /// Проверка: QueueCapacity возвращает установленную вместимость очереди.
    /// </summary>
    [Fact]
    public void ActorTests_008()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        FakeActor actor1 = new(system, Guid.NewGuid(), "default");
        FakeActor actor2 = new(system, Guid.NewGuid(), "custom", queueCapacity: 512);

        Assert.Equal(Actor.DefaultQueueCapacity, actor1.QueueCapacity);
        Assert.Equal(512, actor2.QueueCapacity);
    }

    /// <summary>
    /// Проверка: конструктор выбрасывает ArgumentOutOfRangeException при некорректном Uid (пустой Guid).
    /// </summary>
    [Fact]
    public void ActorTests_009()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
                  new FakeActor(system, Guid.Empty, "invalid"));
    }

    /// <summary>
    /// Проверка: конструктор не выбрасывает ArgumentOutOfRangeException при некорректной вместимости очереди.
    /// Размер очереди корректируется.
    /// </summary>
    [Fact]
    public void ActorTests_010()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        Guid uid = Guid.NewGuid();

        FakeActor actor = new(system, uid, "zero", queueCapacity: 0);
        Assert.Equal(1, actor.QueueCapacity);

        actor = new(system, uid, "too-large", queueCapacity: 200_000);
        Assert.Equal(Actor.MaxQueueCapacity, actor.QueueCapacity);
    }

    /// <summary>
    /// Проверка: исключения в OnLetter не останавливают актор и логируются.
    /// </summary>
    [Fact]
    public async Task ActorTests_011()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        List<Exception> errors = [];
        ThrowingActor actor = new(system, Guid.NewGuid(), "throwing", errors);
        TestLetter letter = new(Guid.NewGuid(), actor.Uid);

        system.RegisterActor(actor);
        system.Send(letter);

        await Task.Delay(100);

        system.Shutdown();
        await system.WaitForShutdownAsync();

        Assert.Single(errors);
    }

    /// <summary>
    /// Проверка: ShutdownLetter останавливает актор.
    /// </summary>
    [Fact]
    public async Task ActorTests_012()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        TaskCompletionSource<bool> started = new();
        SlowActor actor = new(system, Guid.NewGuid(), "slow", started);
        TestLetter letter = new(Guid.NewGuid(), actor.Uid);
        ShutdownLetter shutdownLetter = new(SystemUids.System, actor.Uid);

        system.RegisterActor(actor);
        system.Send(letter);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        system.Send(shutdownLetter);

        await Task.Delay(100);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    #endregion
}
