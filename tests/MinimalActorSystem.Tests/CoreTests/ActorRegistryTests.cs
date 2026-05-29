namespace MinimalActorSystem.Tests.Core;

public sealed class ActorRegistryTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region TestActors

    private sealed class FakeActor(IActorSystem system, Guid uid, string name, int queueCapacity = Actor.DefaultQueueCapacity)
        : Actor(system, uid, name, queueCapacity)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    #endregion

    #region Tests

    /// <summary>
    /// Проверка: RegisterActor добавляет актор в реестр.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_001()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        FakeActor actor = new(system, Guid.NewGuid(), "test");
        system.RegisterActor(actor);

        Assert.Single(system.GetAllActors());
    }

    /// <summary>
    /// Проверка: UnregisterActor удаляет актор из реестра.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_002()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        FakeActor actor = new(system, Guid.NewGuid(), "test");
        system.RegisterActor(actor);
        system.UnregisterActor(actor.Uid);

        Assert.Equal(0, system.ActorCount);
    }

    /// <summary>
    /// Проверка: FindActor возвращает зарегистрированного актора.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_003()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        FakeActor actor = new(system, Guid.NewGuid(), "test");
        system.RegisterActor(actor);
        Actor? found = system.FindActor(actor.Uid);

        Assert.Same(actor, found);
    }

    /// <summary>
    /// Проверка: FindActor возвращает null для незарегистрированного Uid.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_004()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        Actor? found = system.FindActor(Guid.NewGuid());

        Assert.Null(found);
    }

    /// <summary>
    /// Проверка: GetActorName возвращает имя зарегистрированного актора.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_005()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        FakeActor actor = new(system, Guid.NewGuid(), "test-actor");
        system.RegisterActor(actor);
        string name = system.GetActorName(actor.Uid);

        Assert.Equal("test-actor", name);
    }

    /// <summary>
    /// Проверка: GetActorName возвращает строковое представление Guid для незарегистрированного Uid.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_006()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        Guid uid = Guid.NewGuid();
        string name = system.GetActorName(uid);

        Assert.Equal(uid.ToString(), name);
    }

    /// <summary>
    /// Проверка: GetAllActors возвращает список всех зарегистрированных акторов.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_007()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        FakeActor actor1 = new(system, Guid.NewGuid(), "a1");
        FakeActor actor2 = new(system, Guid.NewGuid(), "a2");
        system.RegisterActor(actor1);
        system.RegisterActor(actor2);
        List<Actor> all = system.GetAllActors();

        Assert.Contains(actor1, all);
        Assert.Contains(actor2, all);
        Assert.Equal(2, all.Count);
    }

    /// <summary>
    /// Проверка: GetActorName возвращает "System" для SystemUids.System.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_008()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        string name = system.GetActorName(SystemUids.System);

        Assert.Equal("System", name);
    }

    /// <summary>
    /// Проверка: GetActorName возвращает "TimeService" для SystemUids.TimeService.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_009()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        string name = system.GetActorName(SystemUids.TimeService);

        Assert.Equal("TimeService", name);
    }

    /// <summary>
    /// Проверка: ActorCount возвращает правильное количество акторов.
    /// </summary>
    [Fact]
    public void ActorRegistryTests_010()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        Assert.Equal(0, system.ActorCount);

        FakeActor actor1 = new(system, Guid.NewGuid(), "a1");
        FakeActor actor2 = new(system, Guid.NewGuid(), "a2");

        system.RegisterActor(actor1);
        Assert.Equal(1, system.ActorCount);

        system.RegisterActor(actor2);
        Assert.Equal(2, system.ActorCount);

        system.UnregisterActor(actor1.Uid);
        Assert.Equal(1, system.ActorCount);

        system.UnregisterActor(actor2.Uid);
        Assert.Equal(0, system.ActorCount);
    }

    /// <summary>
    /// Проверка: WaitForEmptyAsync завершается немедленно, если реестр пуст.
    /// </summary>
    [Fact]
    public async Task ActorRegistryTests_011()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        Task waitTask = system.WaitForShutdownAsync();
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(100));

        Assert.Equal(waitTask, completedTask);
    }

    /// <summary>
    /// Проверка: WaitForEmptyAsync ожидает, пока все акторы не будут удалены.
    /// </summary>
    [Fact]
    public async Task ActorRegistryTests_012()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);

        FakeActor actor1 = new(system, Guid.NewGuid(), "a1");
        FakeActor actor2 = new(system, Guid.NewGuid(), "a2");

        system.RegisterActor(actor1);
        system.RegisterActor(actor2);

        Task waitTask = system.WaitForShutdownAsync();

        // Задача не должна быть завершена, пока есть акторы
        Task completedTask = await Task.WhenAny(waitTask, Task.Delay(100));
        Assert.NotEqual(waitTask, completedTask);

        // Удаляем акторов
        system.UnregisterActor(actor1.Uid);
        system.UnregisterActor(actor2.Uid);

        // Теперь задача должна завершиться
        Task timeout = Task.Delay(TimeSpan.FromSeconds(1));
        completedTask = await Task.WhenAny(waitTask, timeout);
        Assert.Equal(waitTask, completedTask);
    }

    #endregion
}
