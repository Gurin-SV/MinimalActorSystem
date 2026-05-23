namespace MinimalActorSystem;

/// <summary>
/// Режимы работы сервиса времени
/// </summary>
/// <remarks>
/// Time service operation modes.
/// </remarks>
public enum TimeServiceModes
{
    /// <summary>
    /// Режимы Debug или Release или тестирование с использованием SystemTimeService.
    /// Если в тесте используется VirtualTimeService,требуется установить либо AsyncTests
    /// либо SyncTests. SystemTimeService и VirtualTimeService в своих конструкторах
    /// проверяют правильность установки режима и при ошибке возбуждают исключение.
    /// </summary>
    /// <remarks>
    /// Production mode. Uses SystemTimeService. VirtualTimeService is not allowed in this mode.
    /// </remarks>
    System,
    /// <summary>
    /// Тестирование в режиме с асинхронной посылкой писем. Предназначен только для
    /// тестов c использованием VirtualTimeService.
    /// </summary>
    /// <remarks>
    /// Async testing mode. Uses VirtualTimeService with asynchronous message processing.
    /// </remarks>
    Async,
    /// <summary>
    /// Режим синхронной обработки писем - письма обрабатываются непосредственно в потоке
    /// отправителя. Предназначен только для тестов при использовании VirtualTimeService.
    /// </summary>
    /// <remarks>
    /// Sync testing mode. Uses VirtualTimeService with synchronous message processing
    /// (messages handled in the sender's thread).
    /// </remarks>
    Sync
}

/// <summary>
/// Настройки акторной системы.
/// Передаются в конструктор <see cref="ActorSystem"/> и доступны акторам через <see cref="IActorSystem.Settings"/>.
/// </summary>
/// <remarks>
/// Actor system settings. Passed to the <see cref="ActorSystem"/> constructor.
/// </remarks>
public sealed class Settings
{
    /// <summary>
    /// Режим работы сервиса времени.
    /// </summary>
    public TimeServiceModes TimeServiceModes { get; init; } = TimeServiceModes.System;
}
