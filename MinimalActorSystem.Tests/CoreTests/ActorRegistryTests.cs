namespace MinimalActorSystem.Tests.Core;

public sealed class ActorRegistryTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private sealed class FakeActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override ValueTask OnLetter(Letter letter) => default;
    }

    [Fact]
    public void ActorRegistryTests_001()
    {
        var system = SystemFactory.CreateSystem(_output);

        var actor = new FakeActor(Guid.NewGuid(), "test", system);
        system.RegisterActor(actor);

        Assert.Single(system.GetAllActors());
    }

    [Fact]
    public void ActorRegistryTests_002()
    {
        var system = SystemFactory.CreateSystem(_output);

        var actor = new FakeActor(Guid.NewGuid(), "test", system);
        system.RegisterActor(actor);
        system.UnregisterActor(actor.Uid);

        Assert.Equal(0, system.ActorCount);
    }

    [Fact]
    public void ActorRegistryTests_003()
    {
        var system = SystemFactory.CreateSystem(_output);

        var actor = new FakeActor(Guid.NewGuid(), "test", system);
        system.RegisterActor(actor);
        var found = system.FindActor(actor.Uid);

        Assert.Same(actor, found);
    }

    [Fact]
    public void ActorRegistryTests_004()
    {
        var system = SystemFactory.CreateSystem(_output);

        var found = system.FindActor(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public void ActorRegistryTests_005()
    {
        var system = SystemFactory.CreateSystem(_output);

        var actor = new FakeActor(Guid.NewGuid(), "test-actor", system);
        system.RegisterActor(actor);
        var name = system.GetActorName(actor.Uid);

        Assert.Equal("test-actor", name);
    }

    [Fact]
    public void ActorRegistryTests_006()
    {
        var system = SystemFactory.CreateSystem(_output);

        var uid = Guid.NewGuid();
        var name = system.GetActorName(uid);

        Assert.Equal(uid.ToString(), name);
    }

    [Fact]
    public void ActorRegistryTests_007()
    {
        var system = SystemFactory.CreateSystem(_output);

        var actor1 = new FakeActor(Guid.NewGuid(), "a1", system);
        var actor2 = new FakeActor(Guid.NewGuid(), "a2", system);
        system.RegisterActor(actor1);
        system.RegisterActor(actor2);
        var all = system.GetAllActors();

        Assert.Contains(actor1, all);
        Assert.Contains(actor2, all);
        Assert.Equal(2, all.Count);
    }
}
