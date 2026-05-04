namespace MinimalActorSystem.Tests;

public sealed class SystemTimeServiceTests
{
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

    // Проверка: UtcNow возвращает текущее время
    [Fact]
    public void SystemTimeServiceTests_001()
    {
        var system = new ActorSystem(new Settings());
        var timeService = new SystemTimeService(system);
        system.SetTimeService(timeService);
        var before = DateTime.UtcNow;

        var now = timeService.UtcNow;

        var after = DateTime.UtcNow;
        Assert.InRange(now, before, after);
    }

    // Проверка: Register с TimeSpan не бросает исключений
    [Fact]
    public void SystemTimeServiceTests_002()
    {
        var system = new ActorSystem(new Settings());
        var timeService = new SystemTimeService(system);
        system.SetTimeService(timeService);
        var callback = new TimeoutCallback(Guid.NewGuid(), 1, () => { });

        timeService.Register(TimeSpan.FromSeconds(5), callback);
    }

    // Проверка: по истечении дедлайна отправляется TimeServiceLetter
    [Fact]
    public async Task SystemTimeServiceTests_003()
    {
        var system = new ActorSystem(new Settings());
        var timeService = new SystemTimeService(system);
        system.SetTimeService(timeService);
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(receiverUid, "receiver", system, received, tcs);
        system.RegisterActor(actor);
        system.Start();

        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains(received, l => l is TimeServiceLetter);
    }

    // Проверка: Unregister предотвращает отправку TimeServiceLetter
    [Fact]
    public async Task SystemTimeServiceTests_004()
    {
        var system = new ActorSystem(new Settings());
        var timeService = new SystemTimeService(system);
        system.SetTimeService(timeService);
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(receiverUid, "receiver", system, received, tcs);
        system.RegisterActor(actor);
        system.Start();

        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(100), callback);
        timeService.Unregister(callback);

        await Task.Delay(200);
        Assert.DoesNotContain(received, l => l is TimeServiceLetter);
    }

    // Проверка: повторный Register заменяет старый дедлайн
    [Fact]
    public async Task SystemTimeServiceTests_005()
    {
        var system = new ActorSystem(new Settings());
        var timeService = new SystemTimeService(system);
        system.SetTimeService(timeService);
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(receiverUid, "receiver", system, received, tcs);
        system.RegisterActor(actor);
        system.Start();

        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromSeconds(10), callback);
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(received);
        Assert.IsType<TimeServiceLetter>(received[0]);
    }
}
