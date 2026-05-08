namespace MinimalActorSystem.Testing;

/// <summary>
/// Тестовая реализация <see cref="ILogger"/>, передающая каждое сообщение в пользовательский делегат.
/// Потокобезопасна: вызовы делегата сериализуются через блокировку.
/// Используется в тестах для захвата и проверки логов без записи в файл.
/// </summary>
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
        var line = $"{timestamp:dd.MM.yyyy HH:mm:ss.fff} [{logLevel}] [{Thread.CurrentThread.ManagedThreadId}] {message}";

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
