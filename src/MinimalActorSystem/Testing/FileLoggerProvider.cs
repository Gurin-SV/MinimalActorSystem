namespace MinimalActorSystem.Testing;

/// <summary>
/// Провайдер файлового логгера. Реализует <see cref="ILoggerProvider"/> для интеграции
/// с Microsoft.Extensions.Logging. Создаёт единственный экземпляр <see cref="FileLogger"/>,
/// который разделяется между всеми категориями.
/// </summary>
/// <remarks>
/// File logger provider. Implements ILoggerProvider for Microsoft.Extensions.Logging integration.
/// Creates a single FileLogger instance shared across all categories.
/// </remarks>
/// <param name="filePath">Путь к файлу лога.</param>
/// <param name="system">Акторная система.</param>
/// <param name="minLevel">Минимальный уровень логирования.</param>
public sealed class FileLoggerProvider(string filePath, IActorSystem system, LogLevel minLevel = LogLevel.Trace) : ILoggerProvider
{
    private readonly IActorSystem _system = system;
    private readonly string _filePath = filePath;
    private readonly LogLevel _minLevel = minLevel;
    private FileLogger? _logger;

    /// <summary>
    /// Создаёт или возвращает существующий экземпляр <see cref="FileLogger"/>.
    /// Категория игнорируется — все сообщения пишутся в один файл.
    /// </summary>
    /// <param name="categoryName">Категория логгера (игнорируется).</param>
    /// <returns>Единственный экземпляр <see cref="FileLogger"/> для этого провайдера.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return _logger ??= new FileLogger(_system, _filePath, _minLevel);
    }

    /// <summary>
    /// Освобождает ресурсы, вызывая <see cref="FileLogger.Dispose"/> у созданного логгера.
    /// </summary>
    public void Dispose()
    {
        _logger?.Dispose();
    }
}
