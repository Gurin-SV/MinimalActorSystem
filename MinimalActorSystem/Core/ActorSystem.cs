namespace MinimalActorSystem;

public interface IActorSystem
{
    void Send(Letter letter);
    void RegisterActor(Actor actor);
    void UnregisterActor(Guid uid);
    Task WaitForShutdownAsync();
    CancellationToken CancellationToken { get; }
    SystemUids Uids { get; }
    ILogger Logger { get; }
    ITimeService TimeService { get; }
    Settings Settings { get; }
}

public sealed class ActorSystem(Settings settings) : IActorSystem
{
    private sealed class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }

    private sealed class NullTimeService : ITimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public void Register(DateTime deadline, TimeoutCallback callback) { }
        public void Register(TimeSpan timeout, TimeoutCallback callback) { }
        public void Unregister(TimeoutCallback callback) { }
    }

    private readonly ActorRegistry _registry = new();
    private readonly CancellationTokenSource _cts = new();

    public SystemUids Uids { get; } = new();
    public ILogger Logger { get; private set; } = new NullLogger();
    public ITimeService TimeService { get; private set; } = new NullTimeService();
    public Settings Settings { get; } = settings;
    public CancellationToken CancellationToken => _cts.Token;

    internal int ActorCount => _registry.Count;

    public void SetLogger(ILogger logger)
    {
        Logger = logger;
    }

    public void SetTimeService(ITimeService timeService)
    {
        TimeService = timeService;
    }

    public void RegisterActor(Actor actor)
    {
        _registry.Add(actor);
    }

    public void UnregisterActor(Guid uid)
    {
        _registry.Remove(uid);
    }

    public void Send(Letter letter)
    {
        if (_registry.TryGet(letter.Receiver, out var actor))
        {
            if (Settings.SynchronousProcessing)
                actor.HandleSynchronously(letter);
            else
                actor.TryEnqueue(letter);
        }
    }

    public void Start()
    {
        foreach (var actor in _registry.GetAll())
        {
            _ = actor.RunAsync(CancellationToken);
        }
    }

    public void Shutdown()
    {
        foreach (var actor in _registry.GetAll())
        {
            Send(new ShutdownLetter(Uids.System, actor.Uid));
        }
    }

    public void Panic()
    {
        foreach (var actor in _registry.GetAll())
        {
            actor.HandleSynchronously(new PanicLetter(Uids.System, actor.Uid));
        }
        _cts.Cancel();
    }

    public Task WaitForShutdownAsync() => _registry.WaitForEmptyAsync();
}
