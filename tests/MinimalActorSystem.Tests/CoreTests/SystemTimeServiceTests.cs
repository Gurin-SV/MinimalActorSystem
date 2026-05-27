namespace MinimalActorSystem.Tests.Core;

public sealed class SystemTimeServiceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region TestActors

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

    private sealed class CounterTestActor(IActorSystem system, Guid uid, string name,
        Action<int> onTimeout) : Actor(system, uid, name)
    {
        private int _counter;
        private readonly Action<int> _onTimeout = onTimeout;

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is TimeServiceLetter)
            {
                _counter++;
                _onTimeout(_counter);
            }
            return default;
        }
    }

    #endregion

    #region Tests

    /// <summary>
    /// Проверка: UtcNow возвращает текущее реальное время
    /// </summary>
    [Fact]
    public void SystemTimeServiceTests_001()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        DateTime before = DateTime.UtcNow;

        DateTime now = timeService.UtcNow;

        DateTime after = DateTime.UtcNow;
        Assert.InRange(now, before, after);
    }

    /// <summary>
    /// Проверка: регистрация таймаута не вызывает исключений
    /// </summary>
    [Fact]
    public void SystemTimeServiceTests_002()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        TimeoutCallback callback = new(Guid.NewGuid(), 1, () => { });

        timeService.Register(TimeSpan.FromSeconds(5), callback);
    }

    /// <summary>
    /// Проверка: простой таймаут срабатывает и доставляет письмо актору
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_003()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs = new();
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        TimeoutCallback callback = new(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(20), callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains(received, l => l is TimeServiceLetter);
    }

    /// <summary>
    /// Проверка: отмена таймаута предотвращает срабатывание
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_004()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs = new();
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        TimeoutCallback callback = new(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(100), callback);
        timeService.Unregister(callback);

        // Даём время потенциально сработать
        await Task.Delay(150);
        Assert.DoesNotContain(received, l => l is TimeServiceLetter);
    }

    /// <summary>
    /// Проверка: повторная регистрация с тем же CallbackId заменяет предыдущий таймаут
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_005()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs = new();
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        TimeoutCallback callback = new(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromSeconds(10), callback);
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(received);
        Assert.IsType<TimeServiceLetter>(received[0]);
    }

    /// <summary>
    /// Проверка: регистрация с точным дедлайном срабатывает в нужное время
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_006()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs = new();
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        DateTime deadline = DateTime.UtcNow.AddMilliseconds(100);
        TimeoutCallback callback = new(receiverUid, 1, () => { });
        timeService.Register(deadline, callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains(received, l => l is TimeServiceLetter);
    }

    /// <summary>
    /// Проверка: несколько разных коллбеков для одного актора - оба срабатывают
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_007()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs1 = new();
        TaskCompletionSource<bool> tcs2 = new();
        Guid receiverUid = Guid.NewGuid();

        MultiCallbackTestActor actor = new(system, receiverUid, "receiver", received, tcs1, tcs2);
        system.RegisterActor(actor);

        TimeoutCallback callback1 = new(receiverUid, 1, () => { });
        TimeoutCallback callback2 = new(receiverUid, 2, () => { });

        timeService.Register(TimeSpan.FromMilliseconds(50), callback1);
        timeService.Register(TimeSpan.FromMilliseconds(100), callback2);

        await Task.WhenAll(tcs1.Task.WaitAsync(TimeSpan.FromSeconds(2)),
                           tcs2.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Equal(2, received.Count(l => l is TimeServiceLetter));
    }

    /// <summary>
    /// Проверка: замена таймаута - повторная регистрация с тем же CallbackId заменяет предыдущий
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_008()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs = new();
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        TimeoutCallback callback = new(receiverUid, 1, () => { });

        timeService.Register(TimeSpan.FromSeconds(10), callback);
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(received);
    }

    /// <summary>
    /// Проверка: таймаут не срабатывает после Shutdown системы
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_009()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received);
        system.RegisterActor(actor);

        TimeoutCallback callback = new(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        system.Shutdown();
        await system.WaitForShutdownAsync();

        await Task.Delay(200);
        Assert.DoesNotContain(received, l => l is TimeServiceLetter);
    }

    /// <summary>
    /// Проверка: новые регистрации после Shutdown игнорируются
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_010()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received);
        system.RegisterActor(actor);

        system.Shutdown();
        await system.WaitForShutdownAsync();

        TimeoutCallback callback = new(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.FromMilliseconds(50), callback);

        await Task.Delay(200);
        Assert.DoesNotContain(received, l => l is TimeServiceLetter);
    }

    /// <summary>
    /// Проверка: дедлайн в прошлом - таймаут срабатывает немедленно
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_011()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs = new();
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        DateTime deadline = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(1));
        TimeoutCallback callback = new(receiverUid, 1, () => { });
        timeService.Register(deadline, callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Contains(received, l => l is TimeServiceLetter);
    }

    /// <summary>
    /// Проверка: отмена несуществующего таймаута не вызывает исключений
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_012()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        TimeoutCallback callback = new(Guid.NewGuid(), 1, () => { });

        timeService.Unregister(callback);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Проверка: множественные регистрации разных акторов - все получают свои таймауты
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_013()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;

        List<Letter> received1 = [];
        List<Letter> received2 = [];
        TaskCompletionSource<bool> tcs1 = new();
        TaskCompletionSource<bool> tcs2 = new();

        Guid receiverUid1 = Guid.NewGuid();
        Guid receiverUid2 = Guid.NewGuid();

        TestActor actor1 = new(system, receiverUid1, "receiver1", received1, tcs1);
        TestActor actor2 = new(system, receiverUid2, "receiver2", received2, tcs2);

        system.RegisterActor(actor1);
        system.RegisterActor(actor2);

        TimeoutCallback callback1 = new(receiverUid1, 1, () => { });
        TimeoutCallback callback2 = new(receiverUid2, 1, () => { });

        timeService.Register(TimeSpan.FromMilliseconds(50), callback1);
        timeService.Register(TimeSpan.FromMilliseconds(100), callback2);

        await Task.WhenAll(tcs1.Task.WaitAsync(TimeSpan.FromSeconds(2)),
                           tcs2.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.Contains(received1, l => l is TimeServiceLetter);
        Assert.Contains(received2, l => l is TimeServiceLetter);
    }

    /// <summary>
    /// Проверка: таймаут с нулевой задержкой срабатывает немедленно
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_014()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;
        List<Letter> received = [];
        TaskCompletionSource<bool> tcs = new();
        Guid receiverUid = Guid.NewGuid();
        TestActor actor = new(system, receiverUid, "receiver", received, tcs);
        system.RegisterActor(actor);

        TimeoutCallback callback = new(receiverUid, 1, () => { });
        timeService.Register(TimeSpan.Zero, callback);

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Contains(received, l => l is TimeServiceLetter);
    }

    /// <summary>
    /// Проверка: множество одновременных таймаутов для одного актора - все срабатывают
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_015()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;

        TaskCompletionSource<int> completionTcs = new();
        Guid receiverUid = Guid.NewGuid();

        CounterTestActor actor = new(system, receiverUid, "counter", count =>
        {
            if (count == 5)
                completionTcs.TrySetResult(count);
        });
        system.RegisterActor(actor);

        for (int i = 1; i <= 5; i++)
        {
            TimeoutCallback callback = new(receiverUid, i, () => { });
            timeService.Register(TimeSpan.FromMilliseconds(50), callback);
        }

        int result = await completionTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(5, result);
    }

    /// <summary>
    /// Проверка: Dispose не вызывает исключений и не ломает сервис
    /// (фоновый цикл продолжает работу после Dispose, так как Dispose освобождает только реестр)
    /// </summary>
    [Fact]
    public async Task SystemTimeServiceTests_016()
    {
        ActorSystem system = SystemFactory.CreateSystem(_output);
        SystemTimeService timeService = new(system);
        system.TimeService = timeService;

        // Просто проверяем, что Dispose не падает
        Exception exception = Record.Exception(() => timeService.Dispose());
        Assert.Null(exception);

        // После Dispose сервис может продолжать работу, но проверим хотя бы,
        // что повторный Dispose тоже не падает
        exception = Record.Exception(timeService.Dispose);
        Assert.Null(exception);

        await Task.CompletedTask;
    }

    #endregion
}
