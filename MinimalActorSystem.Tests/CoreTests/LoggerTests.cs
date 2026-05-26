using Microsoft.Extensions.Logging;
using MinimalActorSystem.Testing;

namespace MinimalActorSystem.Tests.Core;

public sealed class LoggerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    #region Tests

    /// <summary>
    /// Проверка файлового логгера: сообщения должны записываться в файл.
    /// </summary>
    [Fact]
    public void LoggerTests_001()
    {
        ActorSystemExtensions.FileLoggerDirectory = Path.GetTempPath();
        var system = new ActorSystem(new Settings());
        system.CreateTestFileLogger();

        system.Logger.LogInformation("Test message 1");
        system.Logger.LogWarning("Test message 2");
        system.Logger.LogError("Test message 3");

        Thread.Sleep(200);
        if (system.Logger is FileLogger logger)
            logger.Dispose();

        if (system.Logger is FileLogger fileLogger)
        {
            Assert.True(File.Exists(fileLogger.FilePath), "Log file should exist");

            var lines = File.ReadAllLines(fileLogger.FilePath);
            Assert.Equal(3, lines.Length);
            Assert.Contains("Information", lines[0]);
            Assert.Contains("Warning", lines[1]);
            Assert.Contains("Error", lines[2]);
        }
    }

    /// <summary>
    /// Проверка callback-логгера: сообщения должны передаваться в делегат
    /// (в тесте проверяется отсутствие исключений, так как вывод идёт в ITestOutputHelper).
    /// </summary>
    [Fact]
    public void LoggerTests_002()
    {
        var system = SystemFactory.CreateSystem(_output);

        system.Logger.LogInformation("Test message 1");
        system.Logger.LogWarning("Test message 2");
        system.Logger.LogError("Test message 3");
    }

    /// <summary>
    /// Проверка NullLogger: вызовы не должны выбрасывать исключений.
    /// </summary>
    [Fact]
    public void LoggerTests_003()
    {
        var system = SystemFactory.CreateSystem();

        system.Logger.LogInformation("Should not throw");
        system.Logger.LogWarning("Should not throw");
        system.Logger.LogError("Should not throw");
    }

    /// <summary>
    /// Проверка фильтрации по уровню логирования: сообщения ниже минимального уровня должны игнорироваться.
    /// </summary>
    [Fact]
    public void LoggerTests_004()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var system = new ActorSystem(new Settings());
            var provider = new FileLoggerProvider(tempFile, system, LogLevel.Warning);
            system.Logger = provider.CreateLogger(string.Empty);

            system.Logger.LogTrace("Trace message - should be ignored");
            system.Logger.LogDebug("Debug message - should be ignored");
            system.Logger.LogInformation("Info message - should be ignored");
            system.Logger.LogWarning("Warning message - should be logged");
            system.Logger.LogError("Error message - should be logged");

            if (system.Logger is FileLogger fileLogger)
                fileLogger.Dispose();

            var lines = File.ReadAllLines(tempFile);
            Assert.Equal(2, lines.Length);
            Assert.Contains("Warning", lines[0]);
            Assert.Contains("Error", lines[1]);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Проверка, что файловый логгер создаёт директорию, если она не существует.
    /// </summary>
    [Fact]
    public void LoggerTests_005()
    {
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        ActorSystemTestExtensions.TestFileLoggerDirectory = testDir;

        try
        {
            var system = new ActorSystem(new Settings());
            system.CreateTestFileLogger(minLevel: LogLevel.Information);

            system.Logger.LogInformation("Test message");

            if (system.Logger is FileLogger fileLogger)
                fileLogger.Dispose();

            Assert.True(Directory.Exists(testDir), "Log directory should be created");

            // Ожидаемое имя файла формируется из имени тестового метода (CallerMemberName)
            var expectedFilePath = Path.Combine(testDir, "LoggerTests_005.log");
            Assert.True(File.Exists(expectedFilePath), $"Log file should exist at {expectedFilePath}");
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    /// <summary>
    /// Проверка, что CallbackLogger корректно форматирует сообщения с исключениями.
    /// </summary>
    [Fact]
    public void LoggerTests_006()
    {
        var loggedLines = new List<string>();
        var system = new ActorSystem(new Settings());
        system.CreateTestLogger(loggedLines.Add, LogLevel.Trace);

        var exception = new InvalidOperationException("Test exception");
        system.Logger.LogError(exception, "Error with exception");

        Assert.Single(loggedLines);
        Assert.Contains("Error with exception", loggedLines[0]);
        Assert.Contains("Test exception", loggedLines[0]);
    }

    /// <summary>
    /// Проверка, что FileLoggerProvider создаёт один экземпляр FileLogger для всех категорий.
    /// </summary>
    [Fact]
    public void LoggerTests_007()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var system = new ActorSystem(new Settings());
            var provider = new FileLoggerProvider(tempFile, system, LogLevel.Trace);

            var logger1 = provider.CreateLogger("Category1");
            var logger2 = provider.CreateLogger("Category2");

            Assert.Same(logger1, logger2);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Проверка, что FileLogger при Dispose сбрасывает буфер и файл содержит все сообщения.
    /// </summary>
    [Fact]
    public void LoggerTests_008()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var system = new ActorSystem(new Settings());
            var logger = new FileLogger(system, tempFile, LogLevel.Trace);

            for (int i = 0; i < 10; i++)
            {
                logger.LogInformation("Message {i}", i);
            }

            logger.Dispose();

            var lines = File.ReadAllLines(tempFile);
            Assert.Equal(10, lines.Length);
            for (int i = 0; i < 10; i++)
            {
                Assert.Contains($"Message {i}", lines[i]);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion
}
