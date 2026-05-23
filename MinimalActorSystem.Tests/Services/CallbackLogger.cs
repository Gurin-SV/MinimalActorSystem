using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MinimalActorSystem.Tests;

/// <summary>
/// Тестовая реализация <see cref="ILogger"/>, передающая каждое сообщение в пользовательский делегат.
/// Потокобезопасна: вызовы делегата сериализуются через блокировку.
/// Используется в тестах для захвата и проверки логов без записи в файл.
/// </summary>
/// <remarks>
/// Test ILogger implementation that passes messages to a user delegate.
/// Thread-safe: delegate calls are serialized via a lock.
/// </remarks>
/// <param name="system">Акторная система для получения временных меток через <see cref="ITimeService.UtcNow"/>.</param>
/// <param name="writeLine">Делегат, принимающий отформатированную строку лога.</param>
/// <param name="minLevel">Минимальный уровень логирования. Сообщения с меньшим уровнем игнорируются.</param>
public sealed class CallbackLogger(IActorSystem system, Action<string> writeLine,
    LogLevel minLevel = LogLevel.Trace)
    : ILogger
{
    private readonly Action<string> _writeLine = writeLine;
    private readonly LogLevel _minLevel = minLevel;
    private readonly IActorSystem _system = system;
    private readonly object _lock = new();
    private readonly Stopwatch sw = Stopwatch.StartNew();

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
        var line = $"{timestamp:dd.MM.yyyy HH:mm:ss} {sw.ElapsedMilliseconds:D4} [{logLevel}] [{Environment.CurrentManagedThreadId}] {message}";

        if (exception != null)
        {
            line += $"{Environment.NewLine}{exception}";
        }

        lock (_lock)
        {
            _writeLine(line);
        }
    }
}
