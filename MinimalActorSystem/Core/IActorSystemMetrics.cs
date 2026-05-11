using System.Diagnostics.Metrics;

namespace MinimalActorSystem;

/// <summary>
/// Метрики акторной системы. По умолчанию используется NullMetrics.Instance.
/// Реализация SystemMetrics использует System.Diagnostics.Metrics для интеграции
/// с OpenTelemetry, Prometheus и другими сборщиками.
/// </summary>
public interface IActorSystemMetrics
{
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

    /// <summary>
    /// Стандартный Meter из System.Diagnostics.Metrics.
    /// Акторы могут использовать его для создания собственных инструментов.
    /// null, если метрики не поддерживаются реализацией.
    /// </summary>
    Meter? Meter { get; }
}
