using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;

namespace MinimalActorSystem.Tests;

/// <summary>
/// Реализация <see cref="ILogger"/>, записывающая логи в файл.
/// Буферизует сообщения в памяти и сбрасывает их на диск пачками с задержкой до 100 мс.
/// Потокобезопасна. При вызове <see cref="Dispose"/> немедленно сбрасывает накопленный буфер.
/// </summary>
/// <remarks>
/// File-based ILogger implementation. Buffers messages in memory and flushes them in batches
/// with up to 100ms delay. Thread-safe. Calls Dispose to flush immediately.
/// </remarks>
public sealed class FileLogger : ILogger, IDisposable
{
    /// <summary>
    /// Путь к файлу лога.
    /// </summary>
    public string FilePath => _filePath;

    private readonly string _filePath;
    private readonly LogLevel _minLevel;
    private readonly IActorSystem _system;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly object _lock = new();
    private Task? _flushTask;
    private volatile bool _disposed;

    /// <summary>
    /// Создаёт экземпляр файлового логгера. При создании очищает файл, если он существует.
    /// </summary>
    /// <param name="system">Акторная система для получения временных меток через <see cref="ITimeService.UtcNow"/>.</param>
    /// <param name="filePath">Путь к файлу лога.</param>
    /// <param name="minLevel">Минимальный уровень логирования. Сообщения с меньшим уровнем игнорируются.</param>
    public FileLogger(IActorSystem system, string filePath, LogLevel minLevel = LogLevel.Trace)
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
            // Не удалось создать или очистить файл — логирование будет недоступно, но не ломает систему
        }
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = _system.TimeService.UtcNow.ToLocalTime();
        var line = $"{timestamp:dd.MM.yyyy HH:mm:ss.fff} [{logLevel}] [{Environment.CurrentManagedThreadId}] {message}";
        _queue.Enqueue(line);
        ScheduleFlush();
    }

    /// <summary>
    /// Планирует отложенную запись накопленных сообщений на диск.
    /// Если запись уже запланирована, новый вызов игнорируется.
    /// Задержка перед сбросом — 100 мс, что позволяет накапливать сообщения и писать их пачкой.
    /// </summary>
    /// <remarks>
    /// Schedules a delayed flush. Ignores duplicate scheduling.
    /// 100ms delay allows batching multiple messages.
    /// </remarks>
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

    /// <summary>
    /// Немедленно сбрасывает все накопленные в очереди сообщения в файл.
    /// Вызывается автоматически по таймеру и при вызове <see cref="Dispose"/>.
    /// </summary>
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
                // Не удалось записать — продолжаем, логирование не должно ломать систему
            }
        }

        lock (_lock)
        {
            _flushTask = null;
        }
    }

    /// <summary>
    /// Освобождает ресурсы и немедленно сбрасывает оставшиеся сообщения на диск.
    /// После вызова новые сообщения не принимаются.
    /// </summary>
    /// <remarks>
    /// Disposes resources and immediately flushes remaining messages to disk.
    /// </remarks>
    public void Dispose()
    {
        _disposed = true;
        Flush();
    }
}
