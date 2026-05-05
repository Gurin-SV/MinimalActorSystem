using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Tests.Core;

public sealed class ActorTests
{
    private sealed class TestLetter(Guid sender, Guid receiver) : Letter(sender, receiver);

    private sealed class FakeActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter) => Task.CompletedTask;
    }

    private sealed class TestActor(Guid uid, string name, IActorSystem system, List<Letter> received)
        : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter)
        {
            received.Add(letter);
            return Task.CompletedTask;
        }
    }

    private sealed class TestActorWithShutdown(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        public bool ShutdownCalled { get; private set; }

        protected override Task OnLetter(Letter letter) => Task.CompletedTask;

        protected override Task OnShutdown()
        {
            ShutdownCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingActor(Guid uid, string name, IActorSystem system)
        : Actor(uid, name, system)
    {
        protected override Task OnLetter(Letter letter)
            => throw new InvalidOperationException("test error");
    }

    private sealed class FakeLogger : ILogger
    {
        public bool HasErrors { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
                HasErrors = true;
        }
    }

    // Проверка: конструктор устанавливает Uid, Name и System
    [Fact]
    public void ActorTests_001()
    {
        var uid = Guid.NewGuid();
        var system = new ActorSystem(new Settings());

        var actor = new FakeActor(uid, "test-actor", system);

        Assert.Equal(uid, actor.Uid);
        Assert.Equal("test-actor", actor.Name);
        Assert.Same(system, actor.System);
    }

    // Проверка: TryEnqueue добавляет письмо в очередь
    [Fact]
    public void ActorTests_002()
    {
        var system = new ActorSystem(new Settings());
        var actor = new FakeActor(Guid.NewGuid(), "test", system);
        var letter = new TestLetter(Guid.NewGuid(), actor.Uid);

        var result = actor.TryEnqueue(letter);

        Assert.True(result);
    }

    // Проверка: RunAsync обрабатывает письма через OnLetter
    [Fact]
    public async Task ActorTests_003()
    {
        var system = new ActorSystem(new Settings());
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

    // Проверка: RunAsync завершается при отмене токена
    [Fact]
    public async Task ActorTests_004()
    {
        var system = new ActorSystem(new Settings());
        var actor = new FakeActor(Guid.NewGuid(), "test", system);
        var cts = new CancellationTokenSource();

        cts.Cancel();
        await actor.RunAsync(cts.Token);
    }

    // Проверка: RunAsync вызывает OnShutdown и UnregisterActor
    [Fact]
    public async Task ActorTests_005()
    {
        var system = new ActorSystem(new Settings());
        var actor = new TestActorWithShutdown(Guid.NewGuid(), "test", system);
        system.RegisterActor(actor);

        var cts = new CancellationTokenSource();
        cts.Cancel();
        await actor.RunAsync(cts.Token);

        Assert.True(actor.ShutdownCalled);
        Assert.Equal(0, system.ActorCount);
    }

    // Проверка: HandleSynchronously вызывает OnLetter
    [Fact]
    public void ActorTests_006()
    {
        var system = new ActorSystem(new Settings());
        var received = new List<Letter>();
        var actor = new TestActor(Guid.NewGuid(), "test", system, received);
        var letter = new TestLetter(Guid.NewGuid(), actor.Uid);

        actor.HandleSynchronously(letter);

        Assert.Single(received);
        Assert.Same(letter, received[0]);
    }

    // Проверка: HandleSynchronously логирует ошибку и не бросает исключение
    [Fact]
    public void ActorTests_007()
    {
        var logger = new FakeLogger();
        var system = new ActorSystem(new Settings());
        system.SetLogger(logger);
        var actor = new ThrowingActor(Guid.NewGuid(), "test", system);
        var letter = new TestLetter(Guid.NewGuid(), actor.Uid);

        actor.HandleSynchronously(letter);

        Assert.True(logger.HasErrors);
    }
}
