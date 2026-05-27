using FluentAssertions;
using MinimalActorSystem.Testing;

namespace MinimalActorSystem.Tests.Core;

public sealed class VirtualTimeServiceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region TestActors

    private sealed class SingleTimerTestActor : Actor
    {
        private readonly TimeoutCallback _callback;
        public int CallbackCount { get; private set; }
        public DateTime CallbackTime { get; private set; }

        public SingleTimerTestActor(IActorSystem system, Guid uid, string name, int callbackId = 1)
            : base(system, uid, name)
        {
            _callback = new TimeoutCallback(uid, callbackId, () =>
            {
                CallbackCount++;
                CallbackTime = System.TimeService.UtcNow;
            });
        }

        public void Register(TimeSpan timeout) => System.TimeService.Register(timeout, _callback);
        public void Register(DateTime deadline) => System.TimeService.Register(deadline, _callback);
        public void Unregister() => System.TimeService.Unregister(_callback);

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is TimeServiceLetter timeLetter)
                timeLetter.Callback.Action();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MultiTimerTestActor(IActorSystem system, Guid uid, string name) : Actor(system, uid, name)
    {
        private readonly Dictionary<int, TimeoutCallback> _callbacks = [];
        public Dictionary<int, int> CallbackCounts { get; } = [];
        public Dictionary<int, DateTime> CallbackTimes { get; } = [];

        public void Register(int callbackId, TimeSpan timeout)
        {
            TimeoutCallback callback = new(Uid, callbackId, () =>
            {
                CallbackCounts.TryGetValue(callbackId, out int count);
                CallbackCounts[callbackId] = count + 1;
                CallbackTimes[callbackId] = System.TimeService.UtcNow;
            });
            _callbacks[callbackId] = callback;
            System.TimeService.Register(timeout, callback);
        }

        public void Unregister(int callbackId)
        {
            if (_callbacks.TryGetValue(callbackId, out var callback))
                System.TimeService.Unregister(callback);
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is TimeServiceLetter timeLetter)
                timeLetter.Callback.Action();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RescheduleTestActor : Actor
    {
        private readonly TimeoutCallback _callback;
        private int _rescheduleCount;
        public int TotalCallbacks { get; private set; }

        public RescheduleTestActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name)
        {
            _callback = new TimeoutCallback(uid, 1, () =>
            {
                TotalCallbacks++;
                if (_rescheduleCount < 3)
                {
                    _rescheduleCount++;
                    System.TimeService.Register(TimeSpan.FromSeconds(1), _callback);
                }
            });
        }

        public void Start() => System.TimeService.Register(TimeSpan.FromSeconds(1), _callback);

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is TimeServiceLetter timeLetter)
                timeLetter.Callback.Action();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SubscriberTestActor(IActorSystem system, Guid uid, string name)
        : Actor(system, uid, name), IVirtualTimeSubscriber
    {
        public List<DateTime> TimeSteps { get; } = [];
        public string SubName => "test-subscriber";

        public ValueTask OnTimeStep(DateTime currentTime)
        {
            TimeSteps.Add(currentTime);
            return ValueTask.CompletedTask;
        }

        protected override ValueTask OnLetter(Letter letter) => ValueTask.CompletedTask;
    }

    #endregion

    #region Helper Methods

    private static DateTime StartTime => "01.01.2024 00:00:00".AsUtc();
    private static DateTime EndTime => "01.01.2024 00:00:10".AsUtc();

    #endregion

    #region Tests

    /// <summary>
    /// Простой таймаут: регистрация на 5 секунд, проверка что сработал один раз в нужное время
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_001(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        SingleTimerTestActor actor = new(system, Guid.NewGuid(), "timer-test");
        system.RegisterActor(actor);

        timeService.SetTime(StartTime);
        actor.Register(TimeSpan.FromSeconds(5));

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, EndTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(StartTime, EndTime, TimeSpan.FromSeconds(1));

        actor.CallbackCount.Should().Be(1);
        actor.CallbackTime.Should().Be("01.01.2024 00:00:05".AsUtc());

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Таймаут с точным дедлайном: регистрация на 7 секунд, проверка срабатывания в указанное время
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_002(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        SingleTimerTestActor actor = new(system, Guid.NewGuid(), "timer-test");
        system.RegisterActor(actor);

        var deadline = "01.01.2024 00:00:07".AsUtc();
        timeService.SetTime(StartTime);
        actor.Register(deadline);

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, EndTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(StartTime, EndTime, TimeSpan.FromSeconds(1));

        actor.CallbackCount.Should().Be(1);
        actor.CallbackTime.Should().Be(deadline);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Перерегистрация таймаута: вторая регистрация заменяет первую, срабатывает только один раз
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_003(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        SingleTimerTestActor actor = new(system, Guid.NewGuid(), "timer-test");
        system.RegisterActor(actor);

        timeService.SetTime(StartTime);
        actor.Register(TimeSpan.FromSeconds(3));
        actor.Register(TimeSpan.FromSeconds(5));

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, EndTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(StartTime, EndTime, TimeSpan.FromSeconds(1));

        actor.CallbackCount.Should().Be(1);
        actor.CallbackTime.Should().Be("01.01.2024 00:00:05".AsUtc());

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Отмена таймаута: после Unregister таймаут не должен сработать
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_004(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        SingleTimerTestActor actor = new(system, Guid.NewGuid(), "timer-test");
        system.RegisterActor(actor);

        timeService.SetTime(StartTime);
        actor.Register(TimeSpan.FromSeconds(3));
        actor.Unregister();

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, EndTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(StartTime, EndTime, TimeSpan.FromSeconds(1));

        actor.CallbackCount.Should().Be(0);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Несколько разных таймаутов: каждый должен сработать в своё время
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_005(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        MultiTimerTestActor actor = new(system, Guid.NewGuid(), "multi-timer");
        system.RegisterActor(actor);

        timeService.SetTime(StartTime);
        actor.Register(1, TimeSpan.FromSeconds(2));
        actor.Register(2, TimeSpan.FromSeconds(5));
        actor.Register(3, TimeSpan.FromSeconds(7));

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, EndTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(StartTime, EndTime, TimeSpan.FromSeconds(1));

        actor.CallbackCounts[1].Should().Be(1);
        actor.CallbackTimes[1].Should().Be("01.01.2024 00:00:02".AsUtc());

        actor.CallbackCounts[2].Should().Be(1);
        actor.CallbackTimes[2].Should().Be("01.01.2024 00:00:05".AsUtc());

        actor.CallbackCounts[3].Should().Be(1);
        actor.CallbackTimes[3].Should().Be("01.01.2024 00:00:07".AsUtc());

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Самоперерегистрация: таймаут перерегистрирует сам себя, должно быть несколько срабатываний
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_006(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        RescheduleTestActor actor = new(system, Guid.NewGuid(), "reschedule-test");
        system.RegisterActor(actor);

        timeService.SetTime(StartTime);
        actor.Start();

        var endTime = "01.01.2024 00:00:10".AsUtc();

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, endTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(StartTime, endTime, TimeSpan.FromSeconds(1));

        actor.TotalCallbacks.Should().Be(4);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Дедлайн в прошлом: при регистрации таймаута с уже прошедшим дедлайном он срабатывает немедленно
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_007(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        SingleTimerTestActor actor = new(system, Guid.NewGuid(), "past-deadline-test");
        system.RegisterActor(actor);

        var currentTime = "01.01.2024 00:00:10".AsUtc();
        timeService.SetTime(currentTime);
        actor.Register("01.01.2024 00:00:05".AsUtc());

        var endTime = "01.01.2024 00:00:15".AsUtc();

        if (synchronousProcessing)
            timeService.StartVirtualClock(currentTime, endTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(currentTime, endTime, TimeSpan.FromSeconds(1));

        actor.CallbackCount.Should().Be(1);
        actor.CallbackTime.Should().Be(currentTime);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Без таймаутов: время всё равно должно продвигаться
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_008(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        timeService.SetTime(StartTime);

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, EndTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(StartTime, EndTime, TimeSpan.FromSeconds(1));

        timeService.UtcNow.Should().BeOnOrAfter(EndTime);
        timeService.UtcNow.Should().BeOnOrBefore(EndTime.Add(TimeSpan.FromSeconds(1)));

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Подписчики: при каждом шаге времени подписчик должен получать уведомление
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_009(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        SubscriberTestActor subscriber = new(system, Guid.NewGuid(), "subscriber");
        system.RegisterActor(subscriber);
        timeService.Subscribe(subscriber);

        timeService.SetTime(StartTime);

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, EndTime, TimeSpan.FromSeconds(2));
        else
            await timeService.StartVirtualClockAsync(StartTime, EndTime, TimeSpan.FromSeconds(2));

        var expectedSteps = new[]
        {
            "01.01.2024 00:00:00".AsUtc(),
            "01.01.2024 00:00:02".AsUtc(),
            "01.01.2024 00:00:04".AsUtc(),
            "01.01.2024 00:00:06".AsUtc(),
            "01.01.2024 00:00:08".AsUtc(),
            "01.01.2024 00:00:10".AsUtc()
        };

        subscriber.TimeSteps.Should().BeEquivalentTo(expectedSteps);

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Одновременные таймауты: все должны сработать в один момент времени
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_010(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        MultiTimerTestActor actor = new(system, Guid.NewGuid(), "simultaneous-test");
        system.RegisterActor(actor);

        timeService.SetTime(StartTime);
        actor.Register(1, TimeSpan.FromSeconds(5));
        actor.Register(2, TimeSpan.FromSeconds(5));
        actor.Register(3, TimeSpan.FromSeconds(5));

        if (synchronousProcessing)
            timeService.StartVirtualClock(StartTime, EndTime, TimeSpan.FromSeconds(1));
        else
            await timeService.StartVirtualClockAsync(StartTime, EndTime, TimeSpan.FromSeconds(1));

        actor.CallbackCounts[1].Should().Be(1);
        actor.CallbackCounts[2].Should().Be(1);
        actor.CallbackCounts[3].Should().Be(1);

        actor.CallbackTimes[1].Should().Be("01.01.2024 00:00:05".AsUtc());
        actor.CallbackTimes[2].Should().Be("01.01.2024 00:00:05".AsUtc());
        actor.CallbackTimes[3].Should().Be("01.01.2024 00:00:05".AsUtc());

        system.Shutdown();
        await system.WaitForShutdownAsync();
    }

    /// <summary>
    /// Остановка системы: при вызове Shutdown виртуальный таймер должен прекратить работу
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTests_011(bool synchronousProcessing)
    {
        var mode = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = mode });
        VirtualTimeService timeService = new(system);
        system.TimeService = timeService;

        SingleTimerTestActor actor = new(system, Guid.NewGuid(), "shutdown-test");
        system.RegisterActor(actor);

        timeService.SetTime(StartTime);
        actor.Register(TimeSpan.FromDays(365));

        Task task;
        if (synchronousProcessing)
        {
            task = Task.Run(() =>
                timeService.StartVirtualClock(StartTime, EndTime.Add(TimeSpan.FromDays(1)), TimeSpan.FromSeconds(1)));
        }
        else
        {
            task = Task.Run(async () =>
                await timeService.StartVirtualClockAsync(StartTime, EndTime.Add(TimeSpan.FromDays(1)), TimeSpan.FromSeconds(1)));
        }

        await Task.Delay(500);

        system.Shutdown();
        await system.WaitForShutdownAsync();

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Ожидаемое исключение при отмене
        }

        actor.CallbackCount.Should().Be(0);
    }

    #endregion
}
