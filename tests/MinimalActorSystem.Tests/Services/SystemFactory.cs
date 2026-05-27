using Microsoft.Extensions.Logging;
using MinimalActorSystem.Testing;

namespace MinimalActorSystem.Tests;

/// <summary>
/// Фабрика для создания экземпляров акторной системы в тестах.
/// </summary>
/// <remarks>
/// Factory for creating actor system instances in tests.
/// </remarks>
public static class SystemFactory
{
    /// <summary>
    /// Создаёт акторную систему с callback-логгером, выводящим сообщения в ITestOutputHelper.
    /// </summary>
    /// <param name="output">Тестовый вывод для логирования.</param>
    /// <param name="settings">Настройки системы (опционально).</param>
    /// <param name="minLevel">Минимальный уровень логирования.</param>
    /// <returns>Настроенная акторная система.</returns>
    /// <remarks>
    /// Creates an actor system with a callback logger that writes to ITestOutputHelper.
    /// </remarks>
    public static ActorSystem CreateSystem(ITestOutputHelper output, Settings? settings = null,
        LogLevel minLevel = LogLevel.Trace)
    {
        ActorSystem system = new(settings ?? new Settings());
        system.CreateTestLogger(output.WriteLine, minLevel);
        return system;
    }

    /// <summary>
    /// Создаёт акторную систему без логгера (используется NullLogger).
    /// </summary>
    /// <param name="settings">Настройки системы (опционально).</param>
    /// <returns>Акторная система.</returns>
    public static ActorSystem CreateSystem(Settings? settings = null)
    {
        return new ActorSystem(settings ?? new Settings());
    }
}
