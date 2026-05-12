using Demo.Common.Philosophers;
using MinimalActorSystem;

namespace Demo.Philosophers_Deadlock;

public sealed class PhilosopherActor : Actor
{
    private readonly Guid _leftForkUid;
    private readonly Guid _rightForkUid;
    private readonly SettingsControl _settingsControl;
    private readonly PhilosophersSnapshot _snapshot;
    private readonly int _index;
    private readonly TimeoutCallback _thinkTimeout;
    private readonly TimeoutCallback _eatTimeout;
    private readonly TimeoutCallback _retryTimeout;
    private State _state = State.Thinking;
    private int _eatCount;
    private int _retryCount;

    public State CurrentState => _state;
    public int EatCount => _eatCount;

    public PhilosopherActor(
        IActorSystem system,
        Guid uid,
        string name,
        Guid leftForkUid,
        Guid rightForkUid,
        SettingsControl settingsControl,
        PhilosophersSnapshot snapshot,
        int index)
        : base(system, uid, name)
    {
        _leftForkUid = leftForkUid;
        _rightForkUid = rightForkUid;
        _settingsControl = settingsControl;
        _snapshot = snapshot;
        _index = index;
        _thinkTimeout = new TimeoutCallback(Uid, 1, OnThinkTimeout);
        _eatTimeout = new TimeoutCallback(Uid, 2, OnEatTimeout);
        _retryTimeout = new TimeoutCallback(Uid, 3, OnRetryTimeout);
    }

    private int CurrentDelay(int baseDelay) => baseDelay / _settingsControl.SpeedMultiplier;

    protected override ValueTask OnLetter(Letter letter)
    {
        switch (letter)
        {
            case TimeServiceLetter timeLetter:
                timeLetter.Callback.Action();
                break;
            case ForkTakenResponseLetter:
                OnForkTakenResponse();
                break;
            case ForkBusyResponseLetter:
                OnForkBusyResponse();
                break;
            case StartLetter:
                OnStartThinking();
                break;
            case StopLetter:
                OnStop();
                break;
        }
        return default;
    }

    public void OnStartThinking()
    {
        _retryCount = 0;
        _state = State.Thinking;
        _snapshot.Philosophers[_index] = _state;
        _settingsControl.UpdatePhilosopherState(_index, "думает");
        var baseDelay = _settingsControl.IsRandomStart
            ? 5000 + Random.Shared.Next(0, 5000)    // 5-10 сек думает
            : 8000;                                 // 8 сек думает
        System.TimeService.Register(TimeSpan.FromMilliseconds(CurrentDelay(baseDelay)), _thinkTimeout);
    }

    private void OnForkTakenResponse()
    {
        _retryCount = 0;
        if (_state == State.WaitingForLeftFork)
        {
            _state = State.WaitingForRightFork;
            _snapshot.Philosophers[_index] = _state;
            _settingsControl.UpdatePhilosopherState(_index, "ждёт правую вилку");
            _settingsControl.Log($"Философ {_index + 1}: взял левую вилку");
            System.Send(new TakeForkRequestLetter(Uid, _rightForkUid));
        }
        else if (_state == State.WaitingForRightFork)
        {
            _state = State.Eating;
            _snapshot.Philosophers[_index] = _state;
            _settingsControl.UpdatePhilosopherState(_index, $"ест (порция {_eatCount + 1})");
            _settingsControl.Log($"Философ {_index + 1}: взял правую вилку, ест");
            _eatCount++;
            System.TimeService.Register(TimeSpan.FromMilliseconds(CurrentDelay(800)), _eatTimeout);
        }
    }

    private void OnForkBusyResponse()
    {
        if (_state == State.WaitingForLeftFork || _state == State.WaitingForRightFork)
        {
            _retryCount++;
            if (_retryCount == 3)
            {
                _settingsControl.Log($"Философ {_index + 1}: долго ждёт вилку");
            }
            if (_retryCount >= 5)
            {
                if (_state != State.Starving)
                {
                    var oldState = _state;
                    _state = State.Starving;
                    _snapshot.Philosophers[_index] = _state;
                    _settingsControl.UpdatePhilosopherState(_index, "голодает");
                    _settingsControl.Log($"Философ {_index + 1}: ГОЛОДАЕТ (5+ попыток, ждал {(oldState == State.WaitingForLeftFork ? "левую" : "правую")})");
                }
            }
            System.TimeService.Register(TimeSpan.FromMilliseconds(CurrentDelay(500)), _retryTimeout);
        }
    }

    private void OnThinkTimeout()
    {
        _state = State.WaitingForLeftFork;
        _snapshot.Philosophers[_index] = _state;
        _settingsControl.UpdatePhilosopherState(_index, "ждёт левую вилку");
        _settingsControl.Log($"Философ {_index + 1}: проголодался, берёт левую вилку");
        System.Send(new TakeForkRequestLetter(Uid, _leftForkUid));
    }

    private void OnEatTimeout()
    {
        System.Send(new PutForkRequestLetter(Uid, _leftForkUid));
        System.Send(new PutForkRequestLetter(Uid, _rightForkUid));

        _state = State.Thinking;
        _snapshot.Philosophers[_index] = _state;
        _settingsControl.UpdatePhilosopherState(_index, "думает");
        _settingsControl.Log($"Философ {_index + 1}: наелся, отпустил вилки");

        var baseDelay = _settingsControl.IsRandomStart
            ? 5000 + Random.Shared.Next(0, 10_000)
            : 8000;
        System.TimeService.Register(TimeSpan.FromMilliseconds(CurrentDelay(baseDelay)), _thinkTimeout);
    }

    private void OnRetryTimeout()
    {
        if (_state == State.WaitingForLeftFork)
        {
            System.Send(new TakeForkRequestLetter(Uid, _leftForkUid));
        }
        else if (_state == State.WaitingForRightFork || _state == State.Starving)
        {
            System.Send(new TakeForkRequestLetter(Uid, _rightForkUid));
        }
    }

    private void OnStop()
    {
        if (_state == State.Eating || _state == State.WaitingForRightFork)
        {
            System.Send(new PutForkRequestLetter(Uid, _leftForkUid));
            System.Send(new PutForkRequestLetter(Uid, _rightForkUid));
        }
        _state = State.Thinking;
        _snapshot.Philosophers[_index] = _state;
        _settingsControl.UpdatePhilosopherState(_index, "остановлен");
        System.TimeService.Unregister(_thinkTimeout);
        System.TimeService.Unregister(_eatTimeout);
        System.TimeService.Unregister(_retryTimeout);
    }
}
