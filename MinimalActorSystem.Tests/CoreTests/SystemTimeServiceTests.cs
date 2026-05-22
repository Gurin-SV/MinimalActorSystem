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

    [Fact]
    public async Task SystemTimeServiceTests_006()
    {
        /// Регистрация с точным дедлайном: проверка срабатывания в нужное время

        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        var deadline = DateTime.UtcNow.AddMilliseconds(100);
        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(deadline, callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains(received, l => l is TimeServiceLetter);
    }

    [Fact]
    public async Task SystemTimeServiceTests_007()
    {
        /// Несколько разных коллбеков для одного актора: оба должны сработать

        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();

        // Используем два разных TCS для разных коллбеков
        var actor = new MultiCallbackTestActor(system, receiverUid, "receiver", received, tcs1, tcs2);
        system.RegisterActor(actor);

        var callback1 = new TimeoutCallback(receiverUid, 1, () => { });
        var callback2 = new TimeoutCallback(receiverUid, 2, () => { });

        timeService.Register(TimeSpan.FromMilliseconds(50), callback1);
        timeService.Register(TimeSpan.FromMilliseconds(100), callback2);

        await Task.WhenAll(tcs1.Task.WaitAsync(TimeSpan.FromSeconds(2)),
                           tcs2.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Equal(2, received.Count(l => l is TimeServiceLetter));
    }

    private sealed class MultiCallbackTestActor(IActorSystem system, Guid uid, string name,
        List<Letter> received, TaskCompletionSource<bool> tcs1, TaskCompletionSource<bool> tcs2)
        : Actor(system, uid, name)
    {
        private readonly List<Letter> _received = received;
        private readonly TaskCompletionSource<bool> _tcs1 = tcs1;
        private readonly TaskCompletionSource<bool> _tcs2 = tcs2;

        protected override ValueTask OnLetter(Letter letter)
        {
            _received.Add(letter);
            if (letter is TimeServiceLetter timeLetter)
            {
                if (timeLetter.Callback.CallbackId == 1)
                    _tcs1.TrySetResult(true);
                else if (timeLetter.Callback.CallbackId == 2)
                    _tcs2.TrySetResult(true);
            }
            return default;
        }
    }

    [Fact]
    public async Task SystemTimeServiceTests_008()
    {
        /// Замена таймаута: повторная регистрация с тем же CallbackId должна заменить предыдущий

        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        var callback = new TimeoutCallback(receiverUid, 1, () => { });

        // Регистрируем на 10 секунд
        timeService.Register(TimeSpan.FromSeconds(10), callback);
        // Сразу заменяем на 50 мс
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(received);
    }

    [Fact]
    public async Task SystemTimeServiceTests_009()
    {
        /// Таймаут не должен сработать после Shutdown системы

        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(system, receiverUid, "receiver", received);
        system.RegisterActor(actor);

        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        system.Shutdown();
        await system.WaitForShutdownAsync();

        // Даём время потенциально сработать
        await Task.Delay(200);
        Assert.DoesNotContain(received, l => l is TimeServiceLetter);
    }

    [Fact]
    public async Task SystemTimeServiceTests_010()
    {
        /// Новые регистрации после Shutdown должны игнорироваться

        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(system, receiverUid, "receiver", received);
        system.RegisterActor(actor);

        system.Shutdown();
        await system.WaitForShutdownAsync();

        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);
        timeService.Unregister(callback);

        await Task.Delay(200);
        Assert.DoesNotContain(received, l => l is TimeServiceLetter);
    }

    [Fact]
    public async Task SystemTimeServiceTests_011()
    {
        /// Дедлайн в прошлом: таймаут должен сработать немедленно или почти немедленно

        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var received = new List<Letter>();
        var tcs = new TaskCompletionSource<bool>();
        var receiverUid = Guid.NewGuid();
        var actor = new TestActor(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        var deadline = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(1));
        var callback = new TimeoutCallback(receiverUid, 1, () => { });
        timeService.Register(deadline, callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Contains(received, l => l is TimeServiceLetter);
    }

    [Fact]
    public async Task SystemTimeServiceTests_012()
    {
        /// Отмена несуществующего таймаута: не должно быть исключений

        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;
        var callback = new TimeoutCallback(Guid.NewGuid(), 1, () => { });

        // Просто вызываем Unregister для несуществующего коллбека
        timeService.Unregister(callback);

        // Если дошли сюда без исключений - тест пройден
        await Task.CompletedTask;
    }

    [Fact]
    public async Task SystemTimeServiceTests_013()
    {
        /// Множественные регистрации разных акторов - все должны получить свои таймауты

        var system = SystemFactory.CreateSystem(_output);
        var timeService = new SystemTimeService(system);
        system.TimeService = timeService;

        var received1 = new List<Letter>();
        var received2 = new List<Letter>();
        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();

        var receiverUid1 = Guid.NewGuid();
        var receiverUid2 = Guid.NewGuid();

        var actor1 = new TestActor(system, receiverUid1, "receiver1", received1, tcs1);
        var actor2 = new TestActor(system, receiverUid2, "receiver2", received2, tcs2);

        system.RegisterActor(actor1);
        system.RegisterActor(actor2);

        var callback1 = new TimeoutCallback(receiverUid1, 1, () => { });
        var callback2 = new TimeoutCallback(receiverUid2, 1, () => { });

        timeService.Register(TimeSpan.FromMilliseconds(50), callback1);
        timeService.Register(TimeSpan.FromMilliseconds(100), callback2);

        await Task.WhenAll(tcs1.Task.WaitAsync(TimeSpan.FromSeconds(2)),
                           tcs2.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Contains(received1, l => l is TimeServiceLetter);
        Assert.Contains(received2, l => l is TimeServiceLetter);
    }
}
