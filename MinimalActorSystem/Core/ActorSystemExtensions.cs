using System.IO;

namespace MinimalActorSystem;

public static class ActorSystemExtensions
{
    public static string FileLoggerDirectory { get; set; } = Directory.GetCurrentDirectory();
    
    public static void CreateFileLogger(this IActorSystem system, string fileNameWithoutExtension,
        LogLevel minLevel = LogLevel.Trace)
    {
        if (!Directory.Exists(FileLoggerDirectory))
        {
            Directory.CreateDirectory(FileLoggerDirectory);
        }

        var filePath = Path.Combine(FileLoggerDirectory, $"{fileNameWithoutExtension}.log");
        system.Trace($"CreateFileLogger: {filePath} (minLevel={minLevel})");
        var provider = new FileLoggerProvider(filePath, system, minLevel);
        var logger = provider.CreateLogger(string.Empty);
        system.SetLogger(logger);
    }
}
