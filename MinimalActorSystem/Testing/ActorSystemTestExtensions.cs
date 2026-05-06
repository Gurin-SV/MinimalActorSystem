using MinimalActorSystem.Testing;
using System.IO;
using System.Runtime.CompilerServices;

namespace MinimalActorSystem;

public static class ActorSystemTestExtensions
{
    public static string TestFileLoggerDirectory { get; set; }
        = Path.Combine(Directory.GetCurrentDirectory(), "Logs");

    public static void CreateTestFileLogger(this IActorSystem system, LogLevel minLevel = LogLevel.Trace,
        [CallerMemberName] string testMethodName = "")
    {
        if (!Directory.Exists(TestFileLoggerDirectory))
        {
            Directory.CreateDirectory(TestFileLoggerDirectory);
        }

        var filePath = Path.Combine(TestFileLoggerDirectory, $"{testMethodName}.log");
        system.Trace($"CreateTestFileLogger: {filePath} (minLevel={minLevel})");
        var provider = new FileLoggerProvider(filePath, system, minLevel);
        var logger = provider.CreateLogger(string.Empty);
        system.SetLogger(logger);
    }

    public static void CreateTestLogger(this IActorSystem system, Action<string> writeLine,
        LogLevel minLevel = LogLevel.Trace)
    {
        system.Trace($"CreateTestLogger: CallbackLogger (minLevel={minLevel})");
        var logger = new CallbackLogger(system, writeLine, minLevel);
        system.SetLogger(logger);
    }
}
