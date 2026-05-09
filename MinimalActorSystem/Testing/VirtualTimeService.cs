using System.Collections.Concurrent;

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
public class VirtualTimeService(IActorSystem system) : ITimeService
{
    private readonly IActorSystem _system = system;
    private readonly ConcurrentQueue<PendingOp> _pending = new();
    private readonly Dictionary<CallbackKey, TimerEntry> _timers = [];
    private readonly SortedSet<ExpiryEntry> _expiryIndex = [];
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
        _pending.Enqueue(new RegisterOp(deadline, callback));
    }

    /// <inheritdoc/>
    public void Register(TimeSpan timeout, TimeoutCallback callback)
    {
        var deadline = _currentTime + timeout;
        _pending.Enqueue(new RegisterOp(deadline, callback));
    }

    /// <inheritdoc/>
    public void Unregister(TimeoutCallback callback)
    {
        _pending.Enqueue(new UnregisterOp(callback));
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
    /// Синхронный режим.
    /// </summary>
    public void StartVirtualClock(DateTime startTime, DateTime endTime, TimeSpan step)
    {
        _system.Trace("TimeService> Start virtual clock sync");
        _currentTime = startTime;

        while (_currentTime <= endTime)
        {
            _system.Trace("Step time");
            ApplyPendingOps();
            FireSubscribers();
            FireTimeouts();
            _currentTime += step;
        }
        _system.Trace("TimeService> Virtual clock finished");
    }

    /// <summary>
    /// Асинхронный режим. После каждого шага ожидает завершения обработки всех сообщений,
    /// если <see cref="Settings.SynchronousProcessing"/> равен false.
    /// </summary>
    public async Task StartVirtualClockAsync(DateTime startTime, DateTime endTime, TimeSpan step)
    {
        _system.Trace("TimeService> Start virtual clock async");
        _currentTime = startTime;

        while (_currentTime <= endTime)
        {
            _system.Trace("TimeService> Step time");
            ApplyPendingOps();
            await FireSubscribersAsync();
            FireTimeouts();

            if (!_system.Settings.SynchronousProcessing)
            {
#if DEBUG_ACTORS
                await _system.WaitAllIdleAsync();
#endif
            }

            _currentTime += step;
        }
        _system.Trace("TimeService> Virtual clock finished");
    }

    private void ApplyPendingOps()
    {
        while (_pending.TryDequeue(out var op))
        {
            switch (op)
            {
                case RegisterOp reg:
                    {
                        var key = new CallbackKey(reg.Callback.ActorUid, reg.Callback.CallbackId);

                        if (_timers.TryGetValue(key, out var old))
                        {
                            _expiryIndex.Remove(new ExpiryEntry(old.Deadline, key));
                        }

                        _timers[key] = new TimerEntry(reg.Deadline, reg.Callback);
                        _expiryIndex.Add(new ExpiryEntry(reg.Deadline, key));
                        break;
                    }
                case UnregisterOp unreg:
                    {
                        var key = new CallbackKey(unreg.Callback.ActorUid, unreg.Callback.CallbackId);

                        if (_timers.TryGetValue(key, out var old))
                        {
                            _timers.Remove(key);
                            _expiryIndex.Remove(new ExpiryEntry(old.Deadline, key));
                        }
                        break;
                    }
            }
        }
    }

    private void FireTimeouts()
    {
        while (_expiryIndex.Count > 0 && _expiryIndex.Min.Deadline <= _currentTime)
        {
            var entry = _expiryIndex.Min;
            _expiryIndex.Remove(entry);

            if (_timers.TryGetValue(entry.Key, out var timer) && timer.Deadline <= _currentTime)
            {
                _timers.Remove(entry.Key);
                _system.Send(new TimeServiceLetter(
                    SystemUids.TimeService, timer.Callback.ActorUid, timer.Callback));
            }
        }
    }

    private void FireSubscribers()
    {
        foreach (var sub in _subscribers)
        {
            var task = sub.OnTimeStep(_currentTime);
            if (!task.IsCompletedSuccessfully)
                task.AsTask().GetAwaiter().GetResult();
        }
    }

    private async Task FireSubscribersAsync()
    {
        foreach (var sub in _subscribers)
        {
#if DEBUG_ACTORS
            _system.IncrementActivity();
            try
            {
                await sub.OnTimeStep(_currentTime);
            }
            catch (Exception ex)
            {
                _system.Logger.LogError(ex, "Subscriber failed: {Name}", sub.Name);
            }
            finally
            {
                _system.DecrementActivity();
            }
#else
            try
            {
                await sub.OnTimeStep(_currentTime);
            }
            catch (Exception ex)
            {
                _system.Logger.LogError(ex, "Subscriber failed: {Name}", sub.Name);
            }
#endif
        }
    }

    private sealed record CallbackKey(Guid ActorUid, int CallbackId);
    private sealed record TimerEntry(DateTime Deadline, TimeoutCallback Callback);

    private sealed record ExpiryEntry(DateTime Deadline, CallbackKey Key) : IComparable<ExpiryEntry>
    {
        public int CompareTo(ExpiryEntry? other)
        {
            if (other is null)
                return 1;
            
            int cmp = Deadline.CompareTo(other.Deadline);
            if (cmp != 0)
                return cmp;
            
            cmp = Key.ActorUid.CompareTo(other.Key.ActorUid);
            if (cmp != 0)
                return cmp;
            
            return Key.CallbackId.CompareTo(other.Key.CallbackId);
        }
    }

    private abstract record PendingOp;
    private sealed record RegisterOp(DateTime Deadline, TimeoutCallback Callback) : PendingOp;
    private sealed record UnregisterOp(TimeoutCallback Callback) : PendingOp;
}
