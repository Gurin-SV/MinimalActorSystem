using Microsoft.Extensions.Logging;

namespace MinimalActorSystem.Tests.Core;

public sealed class LoggerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void LoggerTests_001_FileLogger()
    {
        ActorSystemExtensions.FileLoggerDirectory = Path.GetTempPath();
        var system = new ActorSystem(new Settings { IsProduction = true });
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

    [Fact]
    public void LoggerTests_002_CallbackLogger()
    {
        var system = SystemFactory.CreateSystem(_output);

        system.Logger.LogInformation("Test message 1");
        system.Logger.LogWarning("Test message 2");
        system.Logger.LogError("Test message 3");
    }

    [Fact]
    public void LoggerTests_003_NullLogger()
    {
        var system = SystemFactory.CreateSystem();

        system.Logger.LogInformation("Should not throw");
        system.Logger.LogWarning("Should not throw");
        system.Logger.LogError("Should not throw");
    }

    [Fact]
    public void LoggerTests_004_TraceToLogger()
    {
        var system = SystemFactory.CreateSystem(_output);

        ((IActorSystem)system).Trace("Trace message 1");
        ((IActorSystem)system).Trace("Trace message 2");
    }
}
