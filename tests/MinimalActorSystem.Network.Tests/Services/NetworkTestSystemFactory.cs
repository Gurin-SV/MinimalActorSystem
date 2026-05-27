using Microsoft.Extensions.Logging;
using MinimalActorSystem.Testing;

namespace MinimalActorSystem.Network.Tests;

/// <summary>
/// Фабрика для создания экземпляров акторной системы для сетевых тестов.
/// </summary>
/// <remarks>
/// Factory for creating actor system instances for network tests.
/// </remarks>
public static class NetworkTestSystemFactory
{
    /// <summary>
    /// Создаёт акторную систему с callback-логгером и фейковым сетевым транспортом.
    /// </summary>
    /// <param name="output">Тестовый вывод для логирования.</param>
    /// <param name="settings">Настройки системы (опционально).</param>
    /// <param name="minLevel">Минимальный уровень логирования.</param>
    /// <returns>Настроенная акторная система.</returns>
    /// <remarks>
    /// Creates an actor system with callback logger and fake network transport.
    /// </remarks>
    public static ActorSystem CreateNetworkSystem(ITestOutputHelper output, Settings? settings = null,
        LogLevel minLevel = LogLevel.Trace)
    {
        ActorSystem system = new(settings ?? new Settings());
        system.CreateTestLogger(output.WriteLine, minLevel);
        return system;
    }

    /// <summary>
    /// Создаёт акторную систему с файловым логгером для сетевых тестов.
    /// </summary>
    /// <param name="testMethodName">Имя тестового метода.</param>
    /// <param name="settings">Настройки системы (опционально).</param>
    /// <param name="minLevel">Минимальный уровень логирования.</param>
    /// <returns>Настроенная акторная система.</returns>
    /// <remarks>
    /// Creates an actor system with file logger for network tests.
    /// </remarks>
    public static ActorSystem CreateNetworkSystemWithFileLogging(string testMethodName,
        Settings? settings = null, LogLevel minLevel = LogLevel.Trace)
    {
        ActorSystem system = new(settings ?? new Settings());
        system.CreateTestFileLogger(minLevel, testMethodName);
        return system;
    }

    /// <summary>
    /// Создаёт акторную систему с SystemTimeService для тестирования таймаутов.
    /// </summary>
    public static ActorSystem CreateNetworkSystemWithRealTime(ITestOutputHelper output,
        Settings? settings = null, LogLevel minLevel = LogLevel.Trace)
    {
        ActorSystem system = new(settings ?? new Settings());
        system.CreateTestLogger(output.WriteLine, minLevel);
        system.TimeService = new SystemTimeService(system);
        return system;
    }
}
