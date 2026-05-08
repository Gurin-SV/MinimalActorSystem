namespace MinimalActorSystem;

/// <summary>
/// Настройки акторной системы. Иммутабельны после создания.
/// Передаются в конструктор <see cref="ActorSystem"/> и доступны акторам через <see cref="IActorSystem.Settings"/>.
/// </summary>
public sealed class Settings
{
    /// <summary>
    /// Флаг продакшен-режима. Влияет на поведение системы (например, уровень логирования).
    /// По умолчанию <c>true</c>.
    /// </summary>
    public bool IsProduction { get; init; } = true;

    /// <summary>
    /// Режим синхронной обработки писем. Если <c>true</c>, письма обрабатываются
    /// непосредственно в потоке отправителя через <see cref="Actor.HandleSynchronously"/>.
    /// Предназначен только для отладки и тестов. В продакшене должен быть <c>false</c>.
    /// По умолчанию <c>false</c>.
    /// </summary>
    public bool SynchronousProcessing { get; init; } = false;
}
