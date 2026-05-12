using Demo.Common.Philosophers;
using MinimalActorSystem;
using MinimalActorSystem.Testing;

namespace Demo.Philosophers_Deadlock;

public sealed class Deadlock : IDemoAlgorithm
{
    private TabPageControl? _page;
    private ActorSystem? _system;
    private PhilosophersSnapshot? _snapshot;
    private PhilosopherActor[]? _philosophers;
    private Guid[]? _forkUids;
    private SettingsControl? _settingsControl;
    private bool _deadlockReported;

    public string Name => "Наивные философы";

    public string Description => "Каждый берёт левую вилку первым.\nЧерез несколько секунд все зависают\nв ожидании правой — классический deadlock";

    public int Index => 0;

    public void Show(TabPage page)
    {
        if (_page == null)
        {
            _page = new TabPageControl { Dock = DockStyle.Fill };
            page.Controls.Add(_page);

            _settingsControl = new SettingsControl { Dock = DockStyle.Fill };
            _page.SplitContainer.Panel2.Controls.Add(_settingsControl);

            _settingsControl.StartClicked += OnStart;
            _settingsControl.StopClicked += OnStop;

            CreateSystem();
        }
    }

    private void OnStart(object? sender, EventArgs e)
    {
        if (_philosophers == null) return;

        _deadlockReported = false;

        // Сброс вилок перед стартом
        for (int i = 0; i < 5; i++)
            _system!.Send(new StopLetter(SystemUids.System, _forkUids![i]));

        var mode = _settingsControl!.IsRandomStart;
        _settingsControl.Log(mode ? "Старт (случайный режим)" : "Старт (одновременный режим)");

        for (int i = 0; i < 5; i++)
        {
            _system!.Send(new StartLetter(SystemUids.System, _philosophers[i].Uid));
        }
    }

    private void OnStop(object? sender, EventArgs e)
    {
        if (_philosophers == null) return;

        _deadlockReported = false;
        _settingsControl!.ClearLog();
        _settingsControl.Log("Стоп");

        // Сначала вилкам — чтобы гарантированно освободились
        for (int i = 0; i < 5; i++)
            _system!.Send(new StopLetter(SystemUids.System, _forkUids![i]));

        // Потом философам
        foreach (var p in _philosophers)
            _system!.Send(new StopLetter(SystemUids.System, p.Uid));
    }

    private void CreateSystem()
    {
        _system?.Shutdown();

        _system = new ActorSystem(new Settings { TimeServiceModes = TimeServiceModes.System });
        _system.TimeService = new SystemTimeService(_system);
        _system.Logger = new CallbackLogger(_system, msg =>
        {
            // Логи системы не пишем в UI-лог, чтобы не засорять
        });

        _snapshot = new PhilosophersSnapshot();

        _forkUids = new Guid[5];
        for (int i = 0; i < 5; i++)
        {
            var fork = new ForkActor(_system, Guid.NewGuid(), $"fork-{i}");
            _forkUids[i] = fork.Uid;
            _system.RegisterActor(fork);
        }

        _philosophers = new PhilosopherActor[5];
        for (int i = 0; i < 5; i++)
        {
            var leftFork = _forkUids[i];
            var rightFork = _forkUids[(i + 1) % 5];
            _philosophers[i] = new PhilosopherActor(
                _system, Guid.NewGuid(), $"philosopher-{i}",
                leftFork, rightFork, _settingsControl!, _snapshot, i);
            _system.RegisterActor(_philosophers[i]);
        }

        _page!.PhilosophersPaintControl.Snapshot = _snapshot;
        _settingsControl!.Log("Система создана. Нажмите Старт.");
    }

    public void Update()
    {
        if (_page == null || _snapshot == null || _system == null || _forkUids == null || _philosophers == null)
            return;

        for (int i = 0; i < 5; i++)
        {
            if (_system.FindActor(_forkUids[i]) is ForkActor fork)
            {
                _snapshot.Forks[i] = fork.IsTaken ? ForkState.Busy : ForkState.Free;
            }
        }

        if (!_deadlockReported)
        {
            bool allStuck = true;
            for (int i = 0; i < 5; i++)
            {
                var state = _philosophers[i].CurrentState;
                if (state == State.Eating || state == State.Thinking)
                {
                    allStuck = false;
                    break;
                }
            }

            if (allStuck)
            {
                _deadlockReported = true;
                _settingsControl!.Log("DEADLOCK! Все философы ждут вилку");
            }
        }

        _page.PhilosophersPaintControl.Invalidate();
    }
}
