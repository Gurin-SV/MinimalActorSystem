namespace MinimalActorSystem;

public sealed class FileLoggerProvider(string filePath, IActorSystem system, LogLevel minLevel = LogLevel.Trace) : ILoggerProvider
{
    private readonly string _filePath = filePath;
    private readonly IActorSystem _system = system;
    private readonly LogLevel _minLevel = minLevel;
    private FileLogger? _logger;

    public ILogger CreateLogger(string categoryName)
    {
        return _logger ??= new FileLogger(_filePath, _system, _minLevel);
    }

    public void Dispose()
    {
        _logger?.Dispose();
    }
}
