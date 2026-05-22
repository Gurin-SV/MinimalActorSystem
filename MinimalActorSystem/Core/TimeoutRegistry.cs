using System.Collections.Concurrent;

namespace MinimalActorSystem;

/// <summary>
/// Реестр таймаутов. Инкапсулирует хранение, регистрацию, отмену
/// и извлечение сработавших таймаутов. Используется реализациями <see cref="ITimeService"/>.
/// Потокобезопасен: регистрация/отмена через ConcurrentQueue, извлечение — через внешнюю синхронизацию.
/// </summary>
/// <remarks>
/// Создаёт реестр таймаутов, связанный с указанной акторной системой.
/// </remarks>
/// <param name="system">Акторная система, чей токен отмены используется для проверок.</param>
internal sealed class TimeoutRegistry(IActorSystem system) : IDisposable
{
    private readonly IActorSystem _system = system;
    private readonly ConcurrentQueue<PendingOp> _pending = new();
    private readonly Dictionary<CallbackKey, TimerEntry> _timers = [];
    private readonly SortedSet<ExpiryEntry> _expiryIndex = [];

    private readonly object _waitLock = new();
    private TaskCompletionSource<bool>? _waitTcs;
    private CancellationTokenRegistration _ctRegistration;

    /// <summary>
    /// Ставит операцию регистрации таймаута в очередь на применение.
    /// </summary>
    public void EnqueueRegister(DateTime deadline, TimeoutCallback callback)
    {
        if (_system.CancellationToken.IsCancellationRequested)
            return;

        _pending.Enqueue(new RegisterOp(deadline, callback));
        SignalChange();
    }

    /// <summary>
    /// Ставит операцию отмены таймаута в очередь на применение.
    /// </summary>
    public void EnqueueUnregister(TimeoutCallback callback)
    {
        if (_system.CancellationToken.IsCancellationRequested)
            return;

        _pending.Enqueue(new UnregisterOp(callback));
        SignalChange();
    }

    /// <summary>
    /// Применяет все накопленные операции регистрации и отмены к основным структурам.
    /// </summary>
    public void ApplyPendingOps()
    {
        while (_pending.TryDequeue(out var op))
        {
            if (op is RegisterOp reg)
            {
                var key = new CallbackKey(reg.Callback.ActorUid, reg.Callback.CallbackId);

                if (_timers.TryGetValue(key, out var old))
                    _expiryIndex.Remove(new ExpiryEntry(old.Deadline, key));

                _timers[key] = new TimerEntry(reg.Deadline, reg.Callback);
                _expiryIndex.Add(new ExpiryEntry(reg.Deadline, key));
            }
            else if (op is UnregisterOp unreg)
            {
                var key = new CallbackKey(unreg.Callback.ActorUid, unreg.Callback.CallbackId);

                if (_timers.TryGetValue(key, out var old))
                {
                    _timers.Remove(key);
                    _expiryIndex.Remove(new ExpiryEntry(old.Deadline, key));
                }
            }
        }
    }

    /// <summary>
    /// Извлекает все таймауты, deadline которых не превышает <paramref name="currentTime"/>.
    /// Сработавшие таймауты удаляются из реестра.
    /// </summary>
    /// <param name="currentTime">Текущее время для проверки.</param>
    /// <returns>Список коллбеков сработавших таймаутов. Может быть пустым.</returns>
    public List<TimeoutCallback> FireTimeouts(DateTime currentTime)
    {
        var fired = new List<TimeoutCallback>();

        while (_expiryIndex.Count > 0 && _expiryIndex.Min.Deadline <= currentTime)
        {
            var entry = _expiryIndex.Min;
            _expiryIndex.Remove(entry);

            if (_timers.TryGetValue(entry.Key, out var timer) && timer.Deadline <= currentTime)
            {
                _timers.Remove(entry.Key);
                fired.Add(timer.Callback);
            }
        }

        return fired;
    }

    /// <summary>
    /// Возвращает ближайший deadline или null, если таймаутов нет.
    /// </summary>
    public DateTime? GetNextDeadline()
    {
        return _expiryIndex.Count > 0 ? _expiryIndex.Min.Deadline : null;
    }

    /// <summary>
    /// Возвращает задачу, которая завершится, когда изменится ближайший дедлайн
    /// или когда будет зарегистрирован/отменён таймаут.
    /// Используется для пробуждения ожидающего цикла.
    /// </summary>
    /// <param name="systemCancellationToken">Токен отмены акторной системы.</param>
    public Task WaitForChangeAsync(CancellationToken systemCancellationToken)
    {
        lock (_waitLock)
        {
            if (systemCancellationToken.IsCancellationRequested)
                return Task.FromCanceled(systemCancellationToken);

            if (_waitTcs == null || _waitTcs.Task.IsCompleted)
            {
                _waitTcs = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                _ctRegistration.Dispose();
                _ctRegistration = systemCancellationToken.Register(() =>
                {
                    lock (_waitLock)
                    {
                        _waitTcs?.TrySetCanceled(systemCancellationToken);
                        _waitTcs = null;
                    }
                });
            }

            return _waitTcs.Task;
        }
    }

    /// <summary>
    /// Сигнализирует об изменении реестра (новая регистрация или отмена).
    /// Пробуждает ожидающие задачи.
    /// </summary>
    private void SignalChange()
    {
        lock (_waitLock)
        {
            _waitTcs?.TrySetResult(true);
            _waitTcs = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ctRegistration.Dispose();
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
