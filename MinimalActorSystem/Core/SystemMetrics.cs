using System.Diagnostics.Metrics;

namespace MinimalActorSystem;

/// <summary>
/// Продакшен-реализация метрик на основе System.Diagnostics.Metrics.
/// Интегрируется с OpenTelemetry, Prometheus и другими сборщиками через стандартный Meter.
/// </summary>
public sealed class SystemMetrics : IActorSystemMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly bool _ownsMeter;
    private readonly Counter<long> _messagesSent;
    private readonly Counter<long> _messagesDropped;
    private readonly Counter<long> _actorsCreated;
    private readonly Counter<long> _actorsDestroyed;
    private int _activeCount;

    /// <inheritdoc/>
    public Meter? Meter => _meter;

    /// <summary>
    /// Создаёт новый Meter с указанным именем. Подходит для сценариев с одной акторной системой.
    /// </summary>
    /// <param name="meterName">Имя Meter. Используется для идентификации источника метрик в OpenTelemetry.</param>
    public SystemMetrics(string meterName = "MinimalActorSystem")
    {
        _meter = new Meter(meterName);
        _ownsMeter = true;

        _messagesSent = _meter.CreateCounter<long>("messages.sent",
            description: "Total messages successfully delivered to actor queues");
        _messagesDropped = _meter.CreateCounter<long>("messages.dropped",
            description: "Messages dropped due to queue overflow");
        _actorsCreated = _meter.CreateCounter<long>("actors.created",
            description: "Total actors created in the system");
        _actorsDestroyed = _meter.CreateCounter<long>("actors.destroyed",
            description: "Total actors destroyed in the system");

        _meter.CreateObservableGauge("actors.active",
            () => _activeCount,
            description: "Current number of active actors in the registry");
    }

    /// <summary>
    /// Использует существующий Meter. Подходит для сценариев с несколькими акторными системами
    /// или когда Meter создаётся централизованно для всех метрик приложения.
    /// </summary>
    /// <param name="meter">Существующий экземпляр Meter. Не может быть null.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если meter равен null.</exception>
    public SystemMetrics(Meter meter)
    {
        _meter = meter ?? throw new ArgumentNullException(nameof(meter));
        _ownsMeter = false;

        _messagesSent = _meter.CreateCounter<long>("messages.sent",
            description: "Total messages successfully delivered to actor queues");
        _messagesDropped = _meter.CreateCounter<long>("messages.dropped",
            description: "Messages dropped due to queue overflow");
        _actorsCreated = _meter.CreateCounter<long>("actors.created",
            description: "Total actors created in the system");
        _actorsDestroyed = _meter.CreateCounter<long>("actors.destroyed",
            description: "Total actors destroyed in the system");

        _meter.CreateObservableGauge("actors.active",
            () => _activeCount,
            description: "Current number of active actors in the registry");
    }

    /// <inheritdoc/>
    public void MessageSent() => _messagesSent.Add(1);

    /// <inheritdoc/>
    public void MessageDropped() => _messagesDropped.Add(1);

    /// <inheritdoc/>
    public void ActorCreated() => _actorsCreated.Add(1);

    /// <inheritdoc/>
    public void ActorDestroyed() => _actorsDestroyed.Add(1);

    /// <inheritdoc/>
    public void ActorCountChanged(int count) => _activeCount = count;

    /// <summary>
    /// Освобождает Meter, если он был создан внутри этого экземпляра.
    /// Если Meter был передан извне — не освобождает.
    /// </summary>
    public void Dispose()
    {
        if (_ownsMeter)
            _meter.Dispose();
    }
}
