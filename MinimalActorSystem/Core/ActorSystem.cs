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
public sealed class ActorSystem : IActorSystem, IActorSystemInternal
{
    /// <summary>
    /// Пустая реализация <see cref="ILogger"/>, используемая по умолчанию. Все вызовы игнорируются.
    /// </summary>
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
    private sealed class NullTimeService : ITimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public void Register(DateTime deadline, TimeoutCallback callback) { }
        public void Register(TimeSpan timeout, TimeoutCallback callback) { }
        public void Unregister(TimeoutCallback callback) { }
    }

    private readonly CancellationTokenSource _cts;
    private readonly ActorRegistry _registry;
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
    }

    /// <inheritdoc/>
    public void RegisterActor(Actor actor)
    {
        _registry.Add(actor);
        _ = actor.RunAsync(CancellationToken);
    }

    /// <inheritdoc/>
    public void UnregisterActor(Guid uid)
    {
        _registry.Remove(uid);
    }

    /// <inheritdoc/>
    public bool Send(Letter letter)
    {
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

        if (!delivered)
        {
            Logger.LogWarning("Send failed: queue full for actor {ActorName}, letter {LetterType} from {SenderName}",
                GetActorName(letter.Receiver), letter.GetType().Name, GetActorName(letter.Sender));
        }

        return delivered;
    }

    /// <inheritdoc/>
    public void Shutdown()
    {
        _cts.Cancel();
    }

    /// <inheritdoc/>
    public void Panic()
    {
        _isPanic = true;
        _cts.Cancel();
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

    void IActorSystemInternal.IncrementActivity()
    {
        if (Settings.TimeServiceModes == TimeServiceModes.Async)
            Interlocked.Increment(ref _activeCount);
    }

    void IActorSystemInternal.DecrementActivity()
    {
        if (Settings.TimeServiceModes == TimeServiceModes.Async)
        {
            if (Interlocked.Decrement(ref _activeCount) == 0)
            {
                _idleTcs?.TrySetResult(true);
            }
        }
    }

    Task IActorSystemInternal.WaitAllIdleAsync()
    {
        if (Settings.TimeServiceModes == TimeServiceModes.Async)
        {
            if (_activeCount == 0)
                return Task.CompletedTask;
            _idleTcs = new TaskCompletionSource<bool>();
            return _idleTcs.Task;
        }
        return Task.CompletedTask;
    }
}
