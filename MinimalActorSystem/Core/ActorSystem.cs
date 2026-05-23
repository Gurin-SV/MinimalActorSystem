using System.Diagnostics.Metrics;

namespace MinimalActorSystem;

internal interface IActorSystemInternal
{
    void IncrementActivity();
    void DecrementActivity();
    Task WaitAllIdleAsync();
}

/// <summary>
/// Реализация акторной системы. Управляет реестром акторов, маршрутизацией писем,
/// жизненным циклом и предоставляет доступ к инфраструктурным сервисам (логгер, время, настройки).
/// </summary>
/// <remarks>
/// Actor system implementation. Manages actor registry, message routing, lifecycle,
/// and infrastructure services.
/// </remarks>
public sealed class ActorSystem : IActorSystem, IActorSystemInternal
{
    /// <summary>
    /// Пустая реализация <see cref="ILogger"/>, используемая по умолчанию. Все вызовы игнорируются.
    /// </summary>
    /// <remarks>
    /// Default no-op logger. Ignores all calls.
    /// </remarks>
    private sealed class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }

    /// <summary>
    /// Пустая реализация <see cref="ITimeService"/>, используемая по умолчанию.
    /// Предоставляет реальное время через <see cref="DateTime.UtcNow"/>, но не обрабатывает таймауты.
    /// </summary>
    /// <remarks>
    /// Default no-op time service. Provides real UTC time but does not handle timeouts.
    /// </remarks>
    private sealed class NullTimeService : ITimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public void Register(DateTime deadline, TimeoutCallback callback) { }
        public void Register(TimeSpan timeout, TimeoutCallback callback) { }
        public void Unregister(TimeoutCallback callback) { }
    }

    /// <summary>
    /// Null-реализация метрик. Все вызовы игнорируются.
    /// Используется по умолчанию, если потребитель не установил свою реализацию.
    /// </summary>
    /// <remarks>
    /// Default no-op metrics. Used when no custom implementation is provided.
    /// </remarks>
    private sealed class NullMetrics : IActorSystemMetrics
    {
        public void MessageSent() { }
        public void MessageDropped() { }
        public void ActorCreated() { }
        public void ActorDestroyed() { }
        public void ActorCountChanged(int count) { }

        public Meter? Meter => null;
    }

    private readonly CancellationTokenSource _cts;
    private readonly ActorRegistry _registry;
    private readonly object _idleLock = new();
    private volatile bool _isPanic;
    private int _activeCount;
    private TaskCompletionSource<bool>? _idleTcs;

    /// <inheritdoc/>
    public Settings Settings { get; }

    /// <inheritdoc/>
    public ILogger Logger { get; set; }

    /// <inheritdoc/>
    public ITimeService TimeService { get; set; }

    /// <inheritdoc/>
    public IActorSystemMetrics Metrics { get; set; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc/>
    public int ActorCount => _registry.Count;

    /// <inheritdoc/>
    public bool IsPanic => _isPanic;

    /// <summary>
    /// Создаёт экземпляр акторной системы с указанными настройками.
    /// Инициализирует реестр акторов, пустые реализации логгера и сервиса времени.
    /// </summary>
    /// <param name="settings">Настройки системы. Иммутабельны после создания.</param>
    public ActorSystem(Settings settings)
    {
        Settings = settings;
        _cts = new();
        CancellationToken = _cts.Token;
        _registry = new(this);
        Logger = new NullLogger();
        TimeService = new NullTimeService();
        Metrics = new NullMetrics();
    }

    /// <inheritdoc/>
    public void RegisterActor(Actor actor)
    {
        _registry.Add(actor);
        Metrics.ActorCreated();
        Metrics.ActorCountChanged(_registry.Count);
        _ = actor.RunAsync(CancellationToken);
    }

    /// <inheritdoc/>
    public void UnregisterActor(Guid uid)
    {
        _registry.Remove(uid);
        Metrics.ActorDestroyed();
        Metrics.ActorCountChanged(_registry.Count);
    }

    /// <inheritdoc/>
    public bool Send(Letter letter)
    {
        if (letter == null)
            return false;
        if (_cts.IsCancellationRequested)
            return false;

        if (!_registry.TryGet(letter.Receiver, out var actor))
        {
            Logger.LogWarning("Send failed: actor {ActorName} not found for letter {LetterType} from {SenderName}",
                GetActorName(letter.Receiver), letter.GetType().Name, GetActorName(letter.Sender));
            return false;
        }

        bool delivered;
        if (Settings.TimeServiceModes == TimeServiceModes.Sync)
        {
            actor.HandleSynchronously(letter);
            delivered = true;
        }
        else
        {
            delivered = actor.TryEnqueue(letter);
        }

        if (delivered)
        {
            Metrics.MessageSent();
        }
        else
        {
            Metrics.MessageDropped();
            Logger.LogWarning("Send failed: queue full for actor {ActorName}, letter {LetterType} from {SenderName}",
                GetActorName(letter.Receiver), letter.GetType().Name, GetActorName(letter.Sender));
        }

        return delivered;
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
        _cts.Cancel();
        if (Metrics is IDisposable disposable)
            disposable.Dispose();
    }

    /// <inheritdoc/>
    public void Panic()
    {
        _isPanic = true;
        Shutdown();
    }

    /// <inheritdoc/>
    public Task WaitForShutdownAsync()
    {
        return _registry.WaitForEmptyAsync();
    }

    /// <inheritdoc/>
    public string GetActorName(Guid uid)
    {
        return _registry.GetName(uid);
    }

    /// <inheritdoc/>
    public List<Actor> GetAllActors()
    {
        return _registry.GetAll();
    }

    /// <inheritdoc/>
    public Actor? FindActor(Guid uid)
    {
        if (_registry.TryGet(uid, out var actor))
            return actor;
        return null;
    }

    /// <remarks>
    /// Increments active actor count. Used only in Async time service mode.
    /// </remarks>
    void IActorSystemInternal.IncrementActivity()
    {
        if (Settings.TimeServiceModes == TimeServiceModes.Async)
            Interlocked.Increment(ref _activeCount);
    }

    /// <remarks>
    /// Decrements active actor count. When count reaches zero, signals WaitAllIdleAsync.
    /// Used only in Async time service mode.
    /// </remarks>
    void IActorSystemInternal.DecrementActivity()
    {
        if (Settings.TimeServiceModes != TimeServiceModes.Async)
            return;

        if (Interlocked.Decrement(ref _activeCount) == 0)
        {
            TaskCompletionSource<bool>? tcs;
            lock (_idleLock)
            {
                tcs = _idleTcs;
                _idleTcs = null;
            }
            tcs?.TrySetResult(true);
        }
    }

    /// <remarks>
    /// Returns a task that completes when all actors are idle (no messages being processed).
    /// Returns CompletedTask immediately if no active messages or in Sync mode.
    /// </remarks>
    Task IActorSystemInternal.WaitAllIdleAsync()
    {
        if (Settings.TimeServiceModes != TimeServiceModes.Async)
            return Task.CompletedTask;

        lock (_idleLock)
        {
            if (_activeCount == 0)
                return Task.CompletedTask;

            if (_idleTcs == null || _idleTcs.Task.IsCompleted)
                _idleTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            return _idleTcs.Task;
        }
    }
}
