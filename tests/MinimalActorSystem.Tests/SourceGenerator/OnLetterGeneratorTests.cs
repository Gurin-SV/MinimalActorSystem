namespace MinimalActorSystem.Tests.SourceGenerator;

#region TestActorsAndLetters

public sealed class PingGeneratedLetter(Guid sender, Guid receiver, int count) : Letter(sender, receiver)
{
    public int Count { get; set; } = count;
}

public sealed class PongGeneratedLetter(Guid sender, Guid receiver, int count) : Letter(sender, receiver)
{
    public int Count { get; set; } = count;
}

[ActorLetterHandler]
public partial class TestGeneratedActor(IActorSystem system, Guid uid, string name) : Actor(system, uid, name)
{
    public int PingReceived { get; private set; }
    public int PongReceived { get; private set; }

    private ValueTask OnPingGeneratedLetter(PingGeneratedLetter ping)
    {
        PingReceived = ping.Count;
        return default;
    }

    private ValueTask OnPongGeneratedLetter(PongGeneratedLetter pong)
    {
        PongReceived = pong.Count;
        return default;
    }
}

#endregion

public sealed class ActorLetterHandlerGeneratorTests
{
    /// <summary>
    /// Проверка: генератор создаёт диспетчеризацию для PingGeneratedLetter.
    /// </summary>
    [Fact]
    public void ActorLetterHandlerGeneratorTests_001()
    {
        Settings settings = new() { TimeServiceModes = TimeServiceModes.Sync };
        ActorSystem system = new(settings);
        TestGeneratedActor actor = new(system, Guid.NewGuid(), "test");
        system.RegisterActor(actor);

        system.Send(new PingGeneratedLetter(SystemUids.System, actor.Uid, 42));

        Assert.Equal(42, actor.PingReceived);
        Assert.Equal(0, actor.PongReceived);
    }

    /// <summary>
    /// Проверка: генератор создаёт диспетчеризацию для PongGeneratedLetter.
    /// </summary>
    [Fact]
    public void ActorLetterHandlerGeneratorTests_002()
    {
        Settings settings = new() { TimeServiceModes = TimeServiceModes.Sync };
        ActorSystem system = new(settings);
        TestGeneratedActor actor = new(system, Guid.NewGuid(), "test");
        system.RegisterActor(actor);

        system.Send(new PongGeneratedLetter(SystemUids.System, actor.Uid, 7));

        Assert.Equal(0, actor.PingReceived);
        Assert.Equal(7, actor.PongReceived);
    }

    /// <summary>
    /// Проверка: генератор игнорирует неизвестные типы писем (например, ShutdownLetter).
    /// </summary>
    [Fact]
    public void ActorLetterHandlerGeneratorTests_003()
    {
        Settings settings = new() { TimeServiceModes = TimeServiceModes.Sync };
        ActorSystem system = new(settings);
        TestGeneratedActor actor = new(system, Guid.NewGuid(), "test");
        system.RegisterActor(actor);

        system.Send(new ShutdownLetter(SystemUids.System, actor.Uid));

        Assert.Equal(0, actor.PingReceived);
        Assert.Equal(0, actor.PongReceived);
    }
}
