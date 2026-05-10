using FluentAssertions;
using MinimalActorSystem.Testing;

namespace MinimalActorSystem.Tests.Self;

public sealed class VirtualTimeServiceTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region TestActors

    private sealed class TimerTestActor : Actor
    {
        private readonly TimeoutCallback _callback;
        public int CallbackCount { get; private set; }
        public DateTime CallbackTime { get; private set; }

        public TimerTestActor(IActorSystem system, Guid uid, string name)
            : base(system, uid, name)
        {
            _callback = new TimeoutCallback(uid, 1, () =>
            {
                CallbackCount++;
                CallbackTime = System.TimeService.UtcNow;
            });
        }

        protected override ValueTask OnLetter(Letter letter)
        {
            if (letter is InitializeLetter)
            {
                System.TimeService.Register(TimeSpan.FromSeconds(5), _callback);
                System.TimeService.Register(TimeSpan.FromSeconds(5), _callback);
            }
            else if (letter is TimeServiceLetter timeLetter)
            {
                timeLetter.Callback.Action();
            }
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VirtualTimeServiceTest001(bool synchronousProcessing)
    {
        TimeServiceModes modes = synchronousProcessing ? TimeServiceModes.Sync : TimeServiceModes.Async;
        var system = SystemFactory.CreateSystem(_output, new Settings { TimeServiceModes = modes });
        var timeService = new VirtualTimeService(system);
        system.TimeService = timeService;

        var actorUid = Guid.NewGuid();
        var actor = new TimerTestActor(system, actorUid, "timer-test");
        system.RegisterActor(actor);

        timeService.SetTime("01.01.2024 00:00:00".AsUtc());
        system.Send(new InitializeLetter(SystemUids.System, actorUid));

        if (synchronousProcessing)
        {
            timeService.StartVirtualClock(
                "01.01.2024 00:00:00".AsUtc(),
                "01.01.2024 00:00:10".AsUtc(),
                TimeSpan.FromSeconds(1));
        }
        else
        {
            await timeService.StartVirtualClockAsync(
                "01.01.2024 00:00:00".AsUtc(),
                "01.01.2024 00:00:10".AsUtc(),
                TimeSpan.FromSeconds(1));
        }

        actor.CallbackCount.Should().Be(1);
        actor.CallbackTime.Should().Be("01.01.2024 00:00:05".AsUtc());
    }
}
