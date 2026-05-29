namespace MinimalActorSystem.Tests.Core;

public sealed class ActorSystemTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region TestActors

    private sealed class FakeActor(IActorSystem system, Guid uid, string name, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    private sealed class TestLetter(Guid sender, Guid receiver)
        : Letter(sender, receiver)
    {
    }

    private sealed class TestActor(IActorSystem system, Guid uid, string name, List<Letter> received,
        TaskCompletionSource<bool>? tcs = null, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
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
        TaskCompletionSource<bool> shutdownTcs, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
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

    private sealed class BlockingActor(IActorSystem system, Guid uid, string name, TaskCompletionSource<bool> started)
        : Actor(system, uid, name)
    {
        private readonly TaskCompletionSource<bool> _started = started;

        protected override async ValueTask OnLetter(Letter letter)
        {
            if (letter is TestLetter)
            {
                _started.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, System.CancellationToken);
            }
        }
    }

    #endregion

    #region Tests

    /// <summary>
    /// Проверка: конструктор ActorSystem сохраняет переданные настройки.
    /// </summary>
    [Fact]
    public void ActorSystemTests_001()
    {
        Settings settings = new();
        ActorSystem system = SystemFactory.CreateSystem(_output, settings);

        Assert.Same(settings, system.Settings);
    }

    /// <summary>
    /// Проверка: RegisterActor увеличивает ActorCount.
    /// </summary>
    [Fact]
    public void ActorSystemTests_002()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        system.RegisterActor(new FakeActor(system, Guid.NewGuid(), "test"));

        Assert.Equal(1, system.ActorCount);
    }

    /// <summary>
    /// Проверка: UnregisterActor уменьшает ActorCount.
    /// </summary>
    [Fact]
    public async Task ActorSystemTests_003()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        Guid uid = Guid.NewGuid();
        system.RegisterActor(new FakeActor(system, uid, "test"));
        system.UnregisterActor(uid);
        await system.WaitForShutdownAsync();

        Assert.Equal(0, system.ActorCount);
    }

    /// <summary>
    /// Проверка: Send доставляет письмо актору-получателю в асинхронном режиме.
    /// </summary>
    [Fact]
    public async Task ActorSystemTests_004()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs = new();
        TestActor actor = new(system, Guid.NewGuid(), "test", received, tcs);
        system.RegisterActor(actor);
        TestLetter letter = new(SystemUids.System, actor.Uid);

        system.Send(letter);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Single(received);
        Assert.Same(letter, received[0]);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Проверка: Send не выбрасывает исключение при отправке письма несуществующему актору.
    /// </summary>
    [Fact]
    public void ActorSystemTests_005()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        // Не должно быть исключения
        system.Send(new TestLetter(SystemUids.System, Guid.NewGuid()));
    }

    /// <summary>
    /// Проверка: Send в синхронном режиме (TimeServiceModes.Sync) обрабатывает письмо немедленно.
    /// </summary>
    [Fact]
    public void ActorSystemTests_006()
    {
        Settings settings = new() { TimeServiceModes = TimeServiceModes.Sync };
        ActorSystem system = SystemFactory.CreateSystem(_output, settings);
        List<Letter> received = [];
        TestActor actor = new(system, Guid.NewGuid(), "test", received);
        system.RegisterActor(actor);
        TestLetter letter = new(SystemUids.System, actor.Uid);

        system.Send(letter);

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }

    /// <summary>
    /// Проверка: ModelActor создаёт дочерних акторов при получении InitializeLetter.
    /// </summary>
    [Fact]
    public async Task ActorSystemTests_007()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        TaskCompletionSource<bool> modelReady = new();
        TestModelActor modelActor = new(system, modelReady);
        system.RegisterActor(modelActor);
        system.Send(new InitializeLetter(SystemUids.System, SystemUids.Model));
        await modelReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // ModelActor + 2 дочерних актора = 3
        Assert.Equal(3, system.ActorCount);
    }

    /// <summary>
    /// Проверка: Shutdown останавливает все акторы и вызывает OnShutdown.
    /// </summary>
    [Fact]
    public async Task ActorSystemTests_008()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        TaskCompletionSource<bool> shutdownTcs = new();
        TestActorWithShutdown actor = new(system, Guid.NewGuid(), "test", shutdownTcs);
        system.RegisterActor(actor);

        system.Shutdown();

        await shutdownTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Проверка: Panic устанавливает IsPanic в true и останавливает систему.
    /// </summary>
    [Fact]
    public async Task ActorSystemTests_009()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        TaskCompletionSource<bool> shutdownTcs = new();
        TestActorWithShutdown actor = new(system, Guid.NewGuid(), "test", shutdownTcs);
        system.RegisterActor(actor);

        system.Panic();

        Assert.True(system.IsPanic);
        await shutdownTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Проверка: FindActor возвращает зарегистрированного актора.
    /// </summary>
    [Fact]
    public void ActorSystemTests_010()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        Guid uid = Guid.NewGuid();
        FakeActor actor = new(system, uid, "test");
        system.RegisterActor(actor);

        Actor? found = system.FindActor(uid);
        Actor? notFound = system.FindActor(Guid.NewGuid());

        Assert.Same(actor, found);
        Assert.Null(notFound);
    }

    /// <summary>
    /// Проверка: GetAllActors возвращает снапшот всех зарегистрированных акторов.
    /// </summary>
    [Fact]
    public void ActorSystemTests_011()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        FakeActor actor1 = new(system, Guid.NewGuid(), "test1");
        FakeActor actor2 = new(system, Guid.NewGuid(), "test2");
        system.RegisterActor(actor1);
        system.RegisterActor(actor2);

        List<Actor> actors = system.GetAllActors();

        Assert.Equal(2, actors.Count);
        Assert.Contains(actor1, actors);
        Assert.Contains(actor2, actors);
    }

    /// <summary>
    /// Проверка: GetActorName возвращает имя зарегистрированного актора или Guid для незарегистрированного.
    /// </summary>
    [Fact]
    public void ActorSystemTests_012()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        Guid uid = Guid.NewGuid();
        FakeActor actor = new(system, uid, "test-actor-name");
        system.RegisterActor(actor);

        string name = system.GetActorName(uid);
        string unknownName = system.GetActorName(Guid.NewGuid());

        Assert.Equal("test-actor-name", name);
        Assert.NotEqual("test-actor-name", unknownName);
    }

    /// <summary>
    /// Проверка: Send возвращает false при отправке письма после Shutdown.
    /// </summary>
    [Fact]
    public async Task ActorSystemTests_013()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        FakeActor actor = new(system, Guid.NewGuid(), "test");
        system.RegisterActor(actor);
        TestLetter letter = new(SystemUids.System, actor.Uid);

        system.Shutdown();
        await system.WaitForShutdownAsync();

        bool result = system.Send(letter);
        Assert.False(result);
    }

    /// <summary>
    /// Проверка: WaitForShutdownAsync ожидает завершения всех акторов.
    /// </summary>
    [Fact]
    public async Task ActorSystemTests_014()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        TaskCompletionSource<bool> started = new();
        BlockingActor actor = new(system, Guid.NewGuid(), "blocking", started);
        TestLetter letter = new(SystemUids.System, actor.Uid);

        system.RegisterActor(actor);
        system.Send(letter);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        system.Shutdown();

        Task shutdownTask = system.WaitForShutdownAsync();
        Task timeout = Task.Delay(TimeSpan.FromSeconds(2));
        Task completed = await Task.WhenAny(shutdownTask, timeout);

        // Блокирующий актор должен завершиться из-за отмены токена
        Assert.Equal(shutdownTask, completed);
    }

    #endregion
}
