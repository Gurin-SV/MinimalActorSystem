namespace MinimalActorSystem;

/// <summary>
/// Продакшен-реализация <see cref="ITimeService"/>, работающая с реальным системным временем.
/// Поддерживает регистрацию, замену и отмену таймаутов. При срабатывании таймаута отправляет
/// актору-получателю <see cref="TimeServiceLetter"/> с зарегистрированным коллбеком.
/// </summary>
public sealed class SystemTimeService : ITimeService, IDisposable
{
    private readonly IActorSystem _system;
    private readonly TimeoutRegistry _registry;
    private readonly Task _runTask;

    /// <inheritdoc/>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Создаёт экземпляр сервиса времени и запускает фоновый цикл обработки таймаутов.
    /// </summary>
    /// <param name="system">Акторная система, в которую будут отправляться <see cref="TimeServiceLetter"/>.</param>
    public SystemTimeService(IActorSystem system)
    {
        _system = system;
        _registry = new TimeoutRegistry(system);
        _runTask = RunAsync(_system.CancellationToken);
    }

    /// <inheritdoc/>
    public void Register(DateTime deadline, TimeoutCallback callback)
    {
        _registry.EnqueueRegister(deadline, callback);
    }

    /// <inheritdoc/>
    public void Register(TimeSpan timeout, TimeoutCallback callback)
    {
        Register(UtcNow + timeout, callback);
    }

    /// <inheritdoc/>
    public void Unregister(TimeoutCallback callback)
    {
        _registry.EnqueueUnregister(callback);
    }

    /// <summary>
    /// Фоновый цикл сервиса времени. Применяет накопленные операции, проверяет индекс
    /// на предмет истёкших таймаутов и ожидает следующего события.
    /// Завершается при отмене <paramref name="ct"/>.
    /// </summary>
    private async Task RunAsync(CancellationToken ct)
    {
        const int maxDelayMs = 1000;

        while (!ct.IsCancellationRequested)
        {
            _registry.ApplyPendingOps();

            var now = UtcNow;
            var fired = _registry.FireTimeouts(now);

            foreach (var callback in fired)
            {
                var letter = new TimeServiceLetter(SystemUids.TimeService, callback.ActorUid, callback);
                _system.Send(letter);
            }

            var nextDeadline = _registry.GetNextDeadline();

            if (nextDeadline.HasValue && nextDeadline.Value > now)
            {
                var delay = nextDeadline.Value - now;
                if (delay > TimeSpan.FromMilliseconds(maxDelayMs))
                    delay = TimeSpan.FromMilliseconds(maxDelayMs);

                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            else if (nextDeadline.HasValue && nextDeadline.Value <= now)
            {
                // Дедлайн уже прошёл — немедленно повторяем цикл
                continue;
            }
            else
            {
                // Нет таймаутов — ждём изменения реестра или отмены системы
                try
                {
                    await _registry.WaitForChangeAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _registry.Dispose();
        // Опционально: дождаться завершения фоновой задачи
        _runTask.Wait(TimeSpan.FromSeconds(5));
    }
}
