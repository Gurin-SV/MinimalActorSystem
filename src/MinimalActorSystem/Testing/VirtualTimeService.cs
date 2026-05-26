namespace MinimalActorSystem.Testing;

/// <summary>
/// Тестовая реализация <see cref="ITimeService"/> с виртуальным временем.
/// Потокобезопасна: регистрация/отмена таймаутов из акторов и управление временем
/// из тестового кода разделены через очередь отложенных операций.
/// </summary>
/// <remarks>
/// Test implementation with virtual time. Thread-safe: registration/unregistration from actors
/// and time control from test code are separated via a pending operations queue.
/// </remarks>
/// <param name="system">Акторная система.</param>
public sealed class VirtualTimeService(ActorSystem system) : ITimeService, IDisposable
{
    private readonly ActorSystem _system = system;
    private readonly TimeoutRegistry _registry = new(system);
    private readonly List<IVirtualTimeSubscriber> _subscribers = [];
    private DateTime _currentTime;

    /// <inheritdoc/>
    public DateTime UtcNow => _currentTime;

    /// <inheritdoc/>
    public void Register(DateTime deadline, TimeoutCallback callback)
    {
        _registry.EnqueueRegister(deadline, callback);
    }

    /// <inheritdoc/>
    public void Register(TimeSpan timeout, TimeoutCallback callback)
    {
        Register(_currentTime + timeout, callback);
    }

    /// <inheritdoc/>
    public void Unregister(TimeoutCallback callback)
    {
        _registry.EnqueueUnregister(callback);
    }

    /// <summary>
    /// Устанавливает текущее виртуальное время без вызова имитаторов и срабатывания таймаутов.
    /// </summary>
    /// <param name="time">Новое виртуальное время.</param>
    /// <remarks>
    /// Sets current virtual time without firing subscribers or timeouts.
    /// </remarks>
    public void SetTime(DateTime time)
    {
        _currentTime = time;
    }

    /// <summary>
    /// Подписывает имитатор на шаги виртуального времени.
    /// </summary>
    /// <param name="subscriber">Подписчик.</param>
    public void Subscribe(IVirtualTimeSubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }

    /// <summary>
    /// Отписывает имитатор от шагов виртуального времени.
    /// </summary>
    /// <param name="subscriber">Подписчик.</param>
    public void Unsubscribe(IVirtualTimeSubscriber subscriber)
    {
        _subscribers.Remove(subscriber);
    }

    /// <summary>
    /// Синхронный режим. Обработчики подписчиков должны быть строго синхронными.
    /// </summary>
    /// <param name="startTime">Начальное виртуальное время.</param>
    /// <param name="endTime">Конечное виртуальное время.</param>
    /// <param name="step">Шаг времени на каждой итерации.</param>
    /// <remarks>
    /// Synchronous mode. Subscriber handlers must be strictly synchronous.
    /// </remarks>
    public void StartVirtualClock(DateTime startTime, DateTime endTime, TimeSpan step)
    {
        _currentTime = startTime;

        while (_currentTime <= endTime && !_system.CancellationToken.IsCancellationRequested)
        {
            _registry.ApplyPendingOps();
            FireSubscribersSync();
            FireTimeouts();
            _currentTime += step;
        }
    }

    /// <summary>
    /// Асинхронный режим. После каждого шага ожидает завершения обработки всех сообщений,
    /// если Settings.TimeServiceModes == TimeServiceModes.Async.
    /// </summary>
    /// <param name="startTime">Начальное виртуальное время.</param>
    /// <param name="endTime">Конечное виртуальное время.</param>
    /// <param name="step">Шаг времени на каждой итерации.</param>
    /// <remarks>
    /// Asynchronous mode. After each step, waits for all messages to be processed
    /// when Settings.TimeServiceModes == TimeServiceModes.Async.
    /// </remarks>
    public async Task StartVirtualClockAsync(DateTime startTime, DateTime endTime, TimeSpan step)
    {
        _currentTime = startTime;

        while (_currentTime <= endTime && !_system.CancellationToken.IsCancellationRequested)
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

    /// <summary>
    /// Срабатывание таймаутов для текущего виртуального времени.
    /// </summary>
    /// <remarks>
    /// Fires timeouts for the current virtual time.
    /// </remarks>
    private void FireTimeouts()
    {
        var fired = _registry.FireTimeouts(_currentTime);
        foreach (var callback in fired)
        {
            _system.Send(new TimeServiceLetter(
                SystemUids.TimeService, callback.ActorUid, callback));
        }
    }

    /// <summary>
    /// Синхронный вызов подписчиков.
    /// </summary>
    /// <remarks>
    /// Synchronous subscriber invocation. Subscribers must return a completed ValueTask.
    /// </remarks>
    private void FireSubscribersSync()
    {
        foreach (var sub in _subscribers)
        {
            var task = sub.OnTimeStep(_currentTime);
            if (!task.IsCompletedSuccessfully)
            {
                // В синхронном режиме подписчики должны возвращать завершённый ValueTask
                try
                {
                    task.AsTask().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _system.Logger.LogError(ex, "Subscriber failed: {Name}", sub.SubName);
                }
            }
        }
    }

    /// <summary>
    /// Асинхронный вызов подписчиков.
    /// </summary>
    /// <remarks>
    /// Asynchronous subscriber invocation. Activity is incremented once for all subscribers.
    /// </remarks>
    private async Task FireSubscribersAsync()
    {
        bool async = _system.Settings.TimeServiceModes == TimeServiceModes.Async;

        // Инкрементируем активность один раз на всех подписчиков
        if (async && _subscribers.Count > 0)
            ((IActorSystemInternal)_system).IncrementActivity();

        try
        {
            foreach (var sub in _subscribers)
            {
                ValueTask task = sub.OnTimeStep(_currentTime);
                if (!task.IsCompletedSuccessfully)
                {
                    try
                    {
                        await task;
                    }
                    catch (Exception ex)
                    {
                        _system.Logger.LogError(ex, "Subscriber failed: {Name}", sub.SubName);
                    }
                }
            }
        }
        finally
        {
            if (async && _subscribers.Count > 0)
                ((IActorSystemInternal)_system).DecrementActivity();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _registry.Dispose();
    }
}
