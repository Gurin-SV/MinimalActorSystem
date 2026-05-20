using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;

namespace MinimalActorSystem.Tests;

/// <summary>
/// Тестовая реализация метрик, выводящая все вызовы в ILogger.
/// Удобно для проверки метрик в тестах — все события видны в логах.
/// </summary>
/// <remarks>
/// Создаёт экземпляр, который пишет метрики в указанный логгер.
/// </remarks>
/// <param name="logger">Логгер, в который выводятся метрики.</param>
public sealed class LoggerMetrics(ILogger logger) : IActorSystemMetrics
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public Meter? Meter => null;

    /// <inheritdoc/>
    public void MessageSent() => _logger.LogTrace("Metric: message.sent");

    /// <inheritdoc/>
    public void MessageDropped() => _logger.LogWarning("Metric: message.dropped");

    /// <inheritdoc/>
    public void ActorCreated() => _logger.LogTrace("Metric: actor.created");

    /// <inheritdoc/>
    public void ActorDestroyed() => _logger.LogTrace("Metric: actor.destroyed");

    /// <inheritdoc/>
    public void ActorCountChanged(int count) => _logger.LogTrace("Metric: actors.active = {Count}", count);
}
