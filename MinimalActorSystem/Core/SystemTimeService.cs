namespace MinimalActorSystem;

/// <summary>
/// Продакшен-реализация <see cref="ITimeService"/>, работающая с реальным системным временем.
/// Поддерживает регистрацию, замену и отмену таймаутов. При срабатывании таймаута отправляет
/// актору-получателю <see cref="TimeServiceLetter"/> с зарегистрированным коллбеком.
/// </summary>
public class SystemTimeService : ITimeService
{
    private readonly IActorSystem _system;
    private readonly TimeoutRegistry _registry = new();

    /// <inheritdoc/>
    public DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Создаёт экземпляр сервиса времени и запускает фоновый цикл обработки таймаутов.
    /// </summary>
    /// <param name="system">Акторная система, в которую будут отправляться <see cref="TimeServiceLetter"/>.</param>
    public SystemTimeService(IActorSystem system)
    {
        _system = system;
        _ = RunAsync(_system.CancellationToken);
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
    /// Фоновый цикл сервиса времени. Периодически применяет накопленные операции регистрации/отмены
    /// и проверяет индекс на предмет истёкших таймаутов. При срабатывании таймаута отправляет
    /// <see cref="TimeServiceLetter"/> соответствующему актору.
    /// Завершается при отмене <paramref name="ct"/>.
    /// </summary>
    /// <param name="ct">Токен отмены от акторной системы.</param>
    private async Task RunAsync(CancellationToken ct)
    {
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

            var delay = TimeSpan.FromMilliseconds(15);
            var nextDeadline = _registry.GetNextDeadline();
            if (nextDeadline.HasValue)
            {
                var timeToNext = nextDeadline.Value - UtcNow;
                if (timeToNext > TimeSpan.Zero && timeToNext < delay)
                    delay = timeToNext;
            }

            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
