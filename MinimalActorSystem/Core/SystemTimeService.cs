using System.Collections.Concurrent;

namespace MinimalActorSystem;

/// <summary>
/// Продакшен-реализация <see cref="ITimeService"/>, работающая с реальным системным временем.
/// Поддерживает регистрацию, замену и отмену таймаутов. При срабатывании таймаута отправляет
/// актору-получателю <see cref="TimeServiceLetter"/> с зарегистрированным коллбеком.
/// </summary>
public sealed class SystemTimeService : ITimeService
{
    private readonly IActorSystem _system;
    private readonly ConcurrentQueue<PendingOp> _pending = [];
    private readonly Dictionary<CallbackKey, TimerEntry> _timers = [];
    private readonly SortedSet<ExpiryEntry> _expiryIndex = [];

    /// <inheritdoc/>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Создаёт экземпляр сервиса времени и запускает фоновый цикл обработки таймаутов.
    /// </summary>
    /// <param name="system">Акторная система, в которую будут отправляться <see cref="TimeServiceLetter"/>.</param>
    public SystemTimeService(IActorSystem system)
    {
        _system = system;
        _system.Trace("SystemTimeService created");
        _ = RunAsync(_system.CancellationToken);
    }

    /// <inheritdoc/>
    public void Register(DateTime deadline, TimeoutCallback callback)
    {
        _system.Trace($"Register timeout: actor={_system.GetActorName(callback.ActorUid)} callbackId={callback.CallbackId} deadline={deadline:HH:mm:ss.fff}");
        _pending.Enqueue(new RegisterOp(deadline, callback));
    }

    /// <inheritdoc/>
    public void Register(TimeSpan timeout, TimeoutCallback callback)
    {
        Register(UtcNow + timeout, callback);
    }

    /// <inheritdoc/>
    public void Unregister(TimeoutCallback callback)
    {
        _system.Trace($"Unregister timeout: actor={_system.GetActorName(callback.ActorUid)} callbackId={callback.CallbackId}");
        _pending.Enqueue(new UnregisterOp(callback));
    }

    /// <summary>
    /// Фоновый цикл сервиса времени. Периодически применяет накопленные операции регистрации/отмены
    /// и проверяет индекс на предмет истёкших таймаутов. При срабатывании таймаута отправляет
    /// <see cref="TimeServiceLetter"/> соответствующему актору.
    /// Завершается при отмене <paramref name="ct"/>.
    /// </summary>
    /// <param name="ct">Токен отмены от акторной системы.</param>
    private async Task RunAsync(CancellationToken ct)
    {
        _system.Trace("loop started");
        while (!ct.IsCancellationRequested)
        {
            ApplyPendingOps();

            var now = UtcNow;
            while (_expiryIndex.Count > 0 && _expiryIndex.Min.Deadline <= now)
            {
                var entry = _expiryIndex.Min;
                _expiryIndex.Remove(entry);

                if (_timers.TryGetValue(entry.Key, out var timer) && timer.Deadline <= now)
                {
                    _timers.Remove(entry.Key);
                    _system.Trace($"Timeout fired: actor={_system.GetActorName(timer.Callback.ActorUid)} callbackId={timer.Callback.CallbackId}");
                    var letter = new TimeServiceLetter(SystemUids.TimeService, timer.Callback.ActorUid, timer.Callback);
                    _system.Send(letter);
                }
            }

            var delay = TimeSpan.FromMilliseconds(15);
            if (_expiryIndex.Count > 0)
            {
                var timeToNext = _expiryIndex.Min.Deadline - UtcNow;
                if (timeToNext > TimeSpan.Zero && timeToNext < delay)
                    delay = timeToNext;
            }

            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                _system.Trace("loop cancelled");
                return;
            }
        }
        _system.Trace("loop finished");
    }

    /// <summary>
    /// Применяет все накопленные в <see cref="_pending"/> операции регистрации и отмены таймаутов
    /// к основным структурам данных. Операции обрабатываются в порядке поступления.
    /// </summary>
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
                            _system.Trace($"Replacing timer: actor={_system.GetActorName(reg.Callback.ActorUid)} callbackId={reg.Callback.CallbackId}");
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

    /// <summary>
    /// Ключ коллбека: пара (ActorUid, CallbackId). Гарантирует уникальность таймаута в рамках одного актора.
    /// </summary>
    private sealed record CallbackKey(Guid ActorUid, int CallbackId);

    /// <summary>
    /// Запись о зарегистрированном таймауте: deadline и связанный коллбек.
    /// </summary>
    private sealed record TimerEntry(DateTime Deadline, TimeoutCallback Callback);

    /// <summary>
    /// Запись в индексе истечения, отсортированном по deadline.
    /// Реализует <see cref="IComparable{T}"/> для использования в <see cref="SortedSet{T}"/>.
    /// При равных deadline сравнение идёт по ActorUid и CallbackId для обеспечения уникальности.
    /// </summary>
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

    /// <summary>
    /// Базовый класс для отложенной операции (регистрация или отмена таймаута).
    /// </summary>
    private abstract record PendingOp;

    /// <summary>
    /// Операция регистрации таймаута.
    /// </summary>
    private sealed record RegisterOp(DateTime Deadline, TimeoutCallback Callback) : PendingOp;

    /// <summary>
    /// Операция отмены таймаута.
    /// </summary>
    private sealed record UnregisterOp(TimeoutCallback Callback) : PendingOp;
}
