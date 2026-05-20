using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Tests;

/// <summary>
/// Методы расширения для <see cref="IActorSystem"/>, упрощающие настройку инфраструктуры.
/// </summary>
public static class ActorSystemExtensions
{
    /// <summary>
    /// Директория для файловых логов. По умолчанию "Logs" относительно текущей рабочей директории.
    /// Может быть изменена до вызова <see cref="CreateFileLogger"/>.
    /// </summary>
    public static string FileLoggerDirectory { get; set; } = "Logs";

    /// <summary>
    /// Создаёт файловый логгер и назначает его акторной системе.
    /// Файл создаётся в директории <see cref="FileLoggerDirectory"/> с расширением .log.
    /// Если директория не существует, она создаётся.
    /// </summary>
    /// <param name="system">Акторная система.</param>
    /// <param name="fileNameWithoutExtension">Имя файла без расширения.</param>
    /// <param name="minLevel">Минимальный уровень логирования. По умолчанию <see cref="LogLevel.Trace"/>.</param>
    public static void CreateFileLogger(this IActorSystem system, string fileNameWithoutExtension,
        LogLevel minLevel = LogLevel.Trace)
    {
        if (!Directory.Exists(FileLoggerDirectory))
        {
            Directory.CreateDirectory(FileLoggerDirectory);
        }

        var filePath = Path.Combine(FileLoggerDirectory, $"{fileNameWithoutExtension}.log");
        var provider = new FileLoggerProvider(filePath, system, minLevel);
        var logger = provider.CreateLogger(string.Empty);
        system.Logger = logger;
    }
}
