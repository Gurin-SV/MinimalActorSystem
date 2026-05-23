namespace MinimalActorSystem.Testing;

/// <summary>
/// Подписчик на шаги виртуального времени.
/// Вызывается сервисом виртуального времени на каждом шаге
/// после применения отложенных операций и до срабатывания таймаутов.
/// Используется для имитации внешней среды: источников телеметрии,
/// модификаторов конфигурации, сценариев отказов и т.п.
/// </summary>
/// <remarks>
/// Subscriber to virtual time steps. Called on each time step after pending operations are applied
/// and before timeouts fire. Used to simulate external environment: telemetry sources,
/// configuration modifiers, failure scenarios, etc.
/// </remarks>
public interface IVirtualTimeSubscriber
{
    /// <summary>
    /// Имя подписчика для диагностики и трассировки.
    /// </summary>
    string SubName { get; }

    /// <summary>
    /// Вызывается при каждом шаге виртуального времени. Обработчики должны быть
    /// строго синхронными во избежание дедлока и возвращать уже завершенный ValueTask
    /// </summary>
    /// <param name="currentTime">Текущее виртуальное время на данном шаге.</param>
    /// <returns>ValueTask для поддержки синхронных и асинхронных реализаций.</returns>
    /// <remarks>
    /// Called on each virtual time step. Handlers must be strictly synchronous
    /// (return a completed ValueTask) to avoid deadlocks.
    /// </remarks>
    ValueTask OnTimeStep(DateTime currentTime);
}
