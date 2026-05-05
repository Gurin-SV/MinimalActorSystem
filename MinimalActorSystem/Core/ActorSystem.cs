namespace MinimalActorSystem;

public interface IActorSystem
{
    void Send(Letter letter);
    void RegisterActor(Actor actor);
    void UnregisterActor(Guid uid);
    Task WaitForShutdownAsync();
    string GetActorName(Guid uid);
    List<Actor> GetAllActors();
    CancellationToken CancellationToken { get; }
    bool IsPanic { get; }
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
    private volatile bool _isPanic;
    private volatile bool _started;

    public SystemUids Uids { get; } = new();
    public ILogger Logger { get; private set; } = new NullLogger();
    public ITimeService TimeService { get; private set; } = new NullTimeService();
    public Settings Settings { get; } = settings;
    public CancellationToken CancellationToken => _cts.Token;
    public bool IsPanic => _isPanic;

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
        if (_started)
            _ = actor.RunAsync(CancellationToken);
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
        _started = true;
        foreach (var actor in _registry.GetAll())
        {
            _ = actor.RunAsync(CancellationToken);
        }
    }

    public void Shutdown()
    {
        _cts.Cancel();
    }

    public void Panic()
    {
        _isPanic = true;
        _cts.Cancel();
    }

    public string GetActorName(Guid uid)
    {
        return _registry.GetName(uid);
    }
    
    public List<Actor> GetAllActors()
    {
        return _registry.GetAll();
    }

    public Task WaitForShutdownAsync() => _registry.WaitForEmptyAsync();

}
