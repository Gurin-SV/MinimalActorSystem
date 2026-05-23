using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace MinimalActorSystem.Tests;

/// <summary>
/// Методы расширения для настройки акторной системы в тестовом окружении.
/// Предоставляет упрощённые способы создания файловых и callback-логгеров.
/// </summary>
/// <remarks>
/// Extension methods for configuring actor system in test environment.
/// Provides simplified ways to create file and callback loggers.
/// </remarks>
public static class ActorSystemTestExtensions
{
    /// <summary>
    /// Директория для файловых логов тестов. По умолчанию — папка "Logs" в текущей рабочей директории.
    /// Может быть изменена до вызова <see cref="CreateTestFileLogger"/>.
    /// </summary>
    public static string TestFileLoggerDirectory { get; set; }
        = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

    /// <summary>
    /// Создаёт файловый логгер для тестового запуска. Имя файла формируется автоматически
    /// по имени тестового метода через <see cref="CallerMemberNameAttribute"/>.
    /// Если директория не существует, она создаётся.
    /// </summary>
    /// <param name="system">Акторная система.</param>
    /// <param name="minLevel">Минимальный уровень логирования. По умолчанию <see cref="LogLevel.Trace"/>.</param>
    /// <param name="testMethodName">Имя тестового метода (подставляется автоматически).</param>
    /// <remarks>
    /// Creates a file logger for test runs. File name is auto-generated from the test method name.
    /// </remarks>
    public static void CreateTestFileLogger(this IActorSystem system, LogLevel minLevel = LogLevel.Trace,
        [CallerMemberName] string testMethodName = "")
    {
        if (!Directory.Exists(TestFileLoggerDirectory))
        {
            Directory.CreateDirectory(TestFileLoggerDirectory);
        }

        var filePath = Path.Combine(TestFileLoggerDirectory, $"{testMethodName}.log");
        var provider = new FileLoggerProvider(filePath, system, minLevel);
        var logger = provider.CreateLogger(string.Empty);
        system.Logger = logger;
    }

    /// <summary>
    /// Создаёт callback-логгер, передающий каждое сообщение в указанный делегат.
    /// Удобен для проверки логов в тестах без создания файлов.
    /// </summary>
    /// <param name="system">Акторная система.</param>
    /// <param name="writeLine">Делегат, принимающий строку лога. Обычно <c>TestContext.WriteLine</c> или <c>Console.WriteLine</c>.</param>
    /// <param name="minLevel">Минимальный уровень логирования. По умолчанию <see cref="LogLevel.Trace"/>.</param>
    /// <remarks>
    /// Creates a callback logger that passes each message to the specified delegate.
    /// Useful for verifying logs in tests without writing to files.
    /// </remarks>
    public static void CreateTestLogger(this IActorSystem system, Action<string> writeLine,
        LogLevel minLevel = LogLevel.Trace)
    {
        var logger = new CallbackLogger(system, writeLine, minLevel);
        system.Logger = logger;
    }
}
