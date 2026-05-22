namespace MinimalActorSystem.Testing;

/// <summary>
/// Тестовая реализация <see cref="ITimeService"/> с виртуальным временем.
/// Потокобезопасна: регистрация/отмена таймаутов из акторов и управление временем
/// из тестового кода разделены через очередь отложенных операций.
/// </summary>
/// <remarks>
/// Создаёт экземпляр сервиса виртуального времени.
/// </remarks>
/// <param name="system">Акторная система.</param>
public class VirtualTimeService(ActorSystem system) : ITimeService
{
    private readonly ActorSystem _system = system;
    private readonly TimeoutRegistry _registry = new();
    private readonly List<IVirtualTimeSubscriber> _subscribers = [];
    private DateTime _currentTime;

    /// <inheritdoc/>
    public DateTime UtcNow => _currentTime;

    /// <summary>
    /// Устанавливает текущее виртуальное время без вызова имитаторов и срабатывания таймаутов.
    /// </summary>
    public void SetTime(DateTime time)
    {
        _currentTime = time;
    }

    /// <inheritdoc/>
    public void Register(DateTime deadline, TimeoutCallback callback)
    {
        _registry.EnqueueRegister(deadline, callback);
    }

    /// <inheritdoc/>
    public void Register(TimeSpan timeout, TimeoutCallback callback)
    {
        _registry.EnqueueRegister(_currentTime + timeout, callback);
    }

    /// <inheritdoc/>
    public void Unregister(TimeoutCallback callback)
    {
        _registry.EnqueueUnregister(callback);
    }

    /// <summary>
    /// Подписывает имитатор на шаги виртуального времени.
    /// </summary>
    public void Subscribe(IVirtualTimeSubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }

    /// <summary>
    /// Отписывает имитатор от шагов виртуального времени.
    /// </summary>
    public void Unsubscribe(IVirtualTimeSubscriber subscriber)
    {
        _subscribers.Remove(subscriber);
    }

    /// <summary>
    /// Синхронный режим. Совмещение синхронного и асинхронного режима не допускается, то есть,
    /// должен вызываться либо StartVirtualClock либо StartVirtualClockAsync, но не оба
    /// </summary>
    public void StartVirtualClock(DateTime startTime, DateTime endTime, TimeSpan step)
    {
        _currentTime = startTime;

        while (_currentTime <= endTime)
        {
            _registry.ApplyPendingOps();
            FireSubscribers();
            FireTimeouts();
            _currentTime += step;
        }
    }

    /// <summary>
    /// Асинхронный режим. После каждого шага ожидает завершения обработки всех сообщений,
    /// если Settings.TimeServiceModes.Async.
    /// </summary>
    public async Task StartVirtualClockAsync(DateTime startTime, DateTime endTime, TimeSpan step)
    {
        _currentTime = startTime;

        while (_currentTime <= endTime)
        {
            _registry.ApplyPendingOps();
            await FireSubscribersAsync();
            FireTimeouts();

            if (_system.Settings.TimeServiceModes == TimeServiceModes.Async)
            {
                await ((IActorSystemInternal)_system).WaitAllIdleAsync();
            }

            _currentTime += step;
        }
    }

    private void FireTimeouts()
    {
        var fired = _registry.FireTimeouts(_currentTime);
        foreach (var callback in fired)
        {
            _system.Send(new TimeServiceLetter(
                SystemUids.TimeService, callback.ActorUid, callback));
        }
    }

    private void FireSubscribers()
    {
        foreach (var sub in _subscribers)
        {
            var task = sub.OnTimeStep(_currentTime);
            if (!task.IsCompletedSuccessfully)
            {
                try
                {
                    task.AsTask().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _system.Logger.LogError(ex, "Subscriber failed: {Name}", sub.Name);
                }
            }
        }
    }

    private async Task FireSubscribersAsync()
    {
        bool async = _system.Settings.TimeServiceModes == TimeServiceModes.Async;

        foreach (var sub in _subscribers)
        {
            ValueTask task = sub.OnTimeStep(_currentTime);
            if (!task.IsCompletedSuccessfully)
            {
                if (async) ((IActorSystemInternal)_system).IncrementActivity();
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    _system.Logger.LogError(ex, "Subscriber failed: {Name}", sub.Name);
                }
                finally
                {
                    if (async) ((IActorSystemInternal)_system).DecrementActivity();
                }
            }
        }
    }
}
