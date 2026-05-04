namespace MinimalActorSystem.Tests;

public sealed class ActorRegistryTests
{
    private sealed class FakeActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter) => Task.CompletedTask;
    }

    // Проверка: добавление актора увеличивает счётчик
    [Fact]
    public void ActorRegistryTests_001()
    {
        var registry = new ActorRegistry();
        var system = new ActorSystem(new Settings());
        var actor = new FakeActor(Guid.NewGuid(), "test", system);

        registry.Add(actor);

        Assert.Equal(1, registry.Count);
    }

    // Проверка: удаление актора уменьшает счётчик
    [Fact]
    public void ActorRegistryTests_002()
    {
        var registry = new ActorRegistry();
        var system = new ActorSystem(new Settings());
        var actor = new FakeActor(Guid.NewGuid(), "test", system);

        registry.Add(actor);
        registry.Remove(actor.Uid);

        Assert.Equal(0, registry.Count);
    }

    // Проверка: TryGet находит добавленного актора
    [Fact]
    public void ActorRegistryTests_003()
    {
        var registry = new ActorRegistry();
        var system = new ActorSystem(new Settings());
        var actor = new FakeActor(Guid.NewGuid(), "test", system);

        registry.Add(actor);
        var found = registry.TryGet(actor.Uid, out var result);

        Assert.True(found);
        Assert.Same(actor, result);
    }

    // Проверка: TryGet возвращает false для отсутствующего актора
    [Fact]
    public void ActorRegistryTests_004()
    {
        var registry = new ActorRegistry();

        var found = registry.TryGet(Guid.NewGuid(), out _);

        Assert.False(found);
    }

    // Проверка: GetName возвращает имя актора
    [Fact]
    public void ActorRegistryTests_005()
    {
        var registry = new ActorRegistry();
        var system = new ActorSystem(new Settings());
        var actor = new FakeActor(Guid.NewGuid(), "test-actor", system);

        registry.Add(actor);
        var name = registry.GetName(actor.Uid);

        Assert.Equal("test-actor", name);
    }

    // Проверка: GetName возвращает строковое представление Uid для отсутствующего
    [Fact]
    public void ActorRegistryTests_006()
    {
        var registry = new ActorRegistry();
        var uid = Guid.NewGuid();

        var name = registry.GetName(uid);

        Assert.Equal(uid.ToString(), name);
    }

    // Проверка: GetAll возвращает всех акторов
    [Fact]
    public void ActorRegistryTests_007()
    {
        var registry = new ActorRegistry();
        var system = new ActorSystem(new Settings());
        var actor1 = new FakeActor(Guid.NewGuid(), "a1", system);
        var actor2 = new FakeActor(Guid.NewGuid(), "a2", system);

        registry.Add(actor1);
        registry.Add(actor2);
        var all = registry.GetAll().ToList();

        Assert.Contains(actor1, all);
        Assert.Contains(actor2, all);
        Assert.Equal(2, all.Count);
    }
}
