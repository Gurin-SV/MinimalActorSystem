using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace MinimalActorSystem;

public sealed class FileLogger : ILogger, IDisposable
{
    public string FilePath => _filePath;

    private readonly string _filePath;
    private readonly LogLevel _minLevel;
    private readonly IActorSystem _system;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly object _lock = new();
    private Task? _flushTask;
    private volatile bool _disposed;

    public FileLogger(string filePath, IActorSystem system, LogLevel minLevel = LogLevel.Trace)
    {
        _filePath = filePath;
        _system = system;
        _minLevel = minLevel;
        try
        {
            File.WriteAllText(_filePath, string.Empty);
        }
        catch
        {

        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = _system.TimeService.UtcNow.ToLocalTime();
        var line = $"{timestamp:dd.MM.yyyy HH:mm:ss.fff} [{logLevel}] [{Thread.CurrentThread.ManagedThreadId}] {message}";
        _queue.Enqueue(line);
        ScheduleFlush();
    }

    private void ScheduleFlush()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_flushTask != null)
                return;

            _flushTask = Task.Run(async () =>
            {
                await Task.Delay(100);
                Flush();
            });
        }
    }

    private void Flush()
    {
        var sb = new StringBuilder();
        while (_queue.TryDequeue(out var line))
        {
            sb.AppendLine(line);
        }

        if (sb.Length > 0)
        {
            try
            {
                File.AppendAllText(_filePath, sb.ToString());
            }
            catch
            {
                // Не удалось записать — ничего страшного, продолжаем
            }
        }

        lock (_lock)
        {
            _flushTask = null;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Flush();
    }
}
