using System.Diagnostics.Metrics;

namespace MinimalActorSystem;

/// <summary>
/// Метрики акторной системы. По умолчанию используется NullMetrics.Instance.
/// Реализация SystemMetrics использует System.Diagnostics.Metrics для интеграции
/// с OpenTelemetry, Prometheus и другими сборщиками.
/// </summary>
/// <remarks>
/// Actor system metrics. Default is a no-op implementation.
/// The built-in implementation uses System.Diagnostics.Metrics for OpenTelemetry/Prometheus integration.
/// </remarks>
public interface IActorSystemMetrics
{
    /// <summary>
    /// Стандартный Meter из System.Diagnostics.Metrics.
    /// Акторы могут использовать его для создания собственных инструментов.
    /// null, если метрики не поддерживаются реализацией.
    /// </summary>
    /// <remarks>
    /// Standard Meter from System.Diagnostics.Metrics. Actors can use it to create custom instruments.
    /// May be null if metrics are not supported.
    /// </remarks>
    Meter? Meter { get; }

    /// <summary>
    /// Вызывается при успешной постановке письма в очередь получателя.
    /// </summary>
    void MessageSent();

    /// <summary>
    /// Вызывается при отбрасывании письма из-за переполнения очереди актора.
    /// </summary>
    void MessageDropped();

    /// <summary>
    /// Вызывается при регистрации нового актора в реестре.
    /// </summary>
    void ActorCreated();

    /// <summary>
    /// Вызывается при удалении актора из реестра.
    /// </summary>
    void ActorDestroyed();

    /// <summary>
    /// Вызывается при изменении количества акторов в реестре.
    /// </summary>
    void ActorCountChanged(int count);
}
