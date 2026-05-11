namespace MinimalActorSystem;

/// <summary>
/// Режимы работы сервиса времени
/// </summary>
public enum TimeServiceModes
{
    /// <summary>
    /// Режимы Debug или Release или тестирование с использованием SystemTimeService.
    /// Если в тесте используется VirtualTimeService,требуется установить либо AsyncTests
    /// либо SyncTests. SystemTimeService и VirtualTimeService в своих конструкторах
    /// проверяют правильность установки режима и при ошибке возбуждают исключение.
    /// </summary>
    System,
    /// <summary>
    /// Тестирование в режиме с асинхронной посылкой писем. Предназначен только для
    /// тестов c использованием VirtualTimeService.
    /// </summary>
    Async,
    /// <summary>
    /// Режим синхронной обработки писем - письма обрабатываются непосредственно в потоке
    /// отправителя. Предназначен только для тестов при использовании VirtualTimeService.
    /// </summary>
    Sync
}

/// <summary>
/// Настройки акторной системы.
/// Передаются в конструктор <see cref="ActorSystem"/> и доступны акторам через <see cref="IActorSystem.Settings"/>.
/// </summary>
public sealed class Settings
{
    /// <summary>
    /// Режим работы сервиса времени.
    /// </summary>
    public TimeServiceModes TimeServiceModes { get; init; } = TimeServiceModes.System;
}
