namespace MinimalActorSystem.Tests.Core;

public sealed class SystemTimeServiceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    private sealed class TestActor(IActorSystem system, Guid uid, string name,
        List<Letter> received, TaskCompletionSource<bool>? tcs = null)
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

    [Fact]
    public void SystemTimeServiceTests_001()
    {
        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var before = DateTime.UtcNow;

        var now = timeService.UtcNow;

        var after = DateTime.UtcNow;
        Assert.InRange(now, before, after);
    }

    [Fact]
    public void SystemTimeServiceTests_002()
    {
        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var callback = new TimeoutCallback(Guid.NewGuid(), 1, () => { });

        timeService.Register(TimeSpan.FromSeconds(5), callback);
    }

    [Fact]
    public async Task SystemTimeServiceTests_003()
    {
        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(20), callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains(received, l => l is TimeServiceLetter);
    }

    [Fact]
    public async Task SystemTimeServiceTests_004()
    {
        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(100), callback);
        timeService.Unregister(callback);

        await Task.Delay(1);
        Assert.DoesNotContain(received, l => l is TimeServiceLetter);
    }

    [Fact]
    public async Task SystemTimeServiceTests_005()
    {
        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromSeconds(10), callback);
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(received);
        Assert.IsType<TimeServiceLetter>(received[0]);
    }
}
